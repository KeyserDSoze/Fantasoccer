using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Group_code_and_group_creation_roll_back_together_if_membership_persistence_fails()
    {
        using var platform = await factory.AccountAsync(globalAdmin: true); using var user = await factory.AccountAsync();
        var profile = (await user.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var issued = (await (await platform.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest("Rollback proof")))
            .Content.ReadFromJsonAsync<IssuedGroupCodeDto>())!;
        var name = "Rollback group " + Guid.NewGuid().ToString("N");
        var trigger = "code_failure_" + Guid.NewGuid().ToString("N");
        // Identifiers and UUID here are generated exclusively by this fixture.
        await using (var database = factory.Database.CreateContext())
        {
            await database.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."UserId" = '{profile.Id}'::uuid THEN RAISE EXCEPTION 'Synthetic group activation failure'; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER {trigger} AFTER INSERT ON "GroupMemberships" FOR EACH ROW EXECUTE FUNCTION {trigger}();
                """);
        }
        try
        {
            HttpResponseMessage? response = null; Exception? failure = null;
            try { response = await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest(name, issued.Code)); }
            catch (Exception cause) { failure = cause; }
            Assert.True(failure != null || response?.StatusCode == HttpStatusCode.InternalServerError);
            await using var database = factory.Database.CreateContext();
            Assert.Empty(await database.Groups.Where(x => x.Name == name).ToArrayAsync());
            Assert.Null((await database.GroupCreationCodes.SingleAsync(x => x.Id == issued.Details.Id)).RedeemedAtUtc);
            Assert.Empty(await database.AccountSelections.Where(x => x.UserId == profile.Id).ToArrayAsync());
        }
        finally
        {
            await using var database = factory.Database.CreateContext();
            await database.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS {trigger} ON \"GroupMemberships\"; DROP FUNCTION IF EXISTS {trigger}();");
        }
        (await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest(name, issued.Code))).EnsureSuccessStatusCode();
    }

    [PostgreSqlFact]
    public async Task Platform_administrator_sees_all_accounts_and_groups_without_inventing_memberships_and_group_owners_cannot_issue_codes()
    {
        using var platform = await factory.AccountAsync(globalAdmin: true);
        using var owner = await factory.AccountAsync();
        using var stranger = await factory.AccountAsync();
        var profile = (await platform.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        Assert.True(profile.IsGlobalAdministrator); Assert.Empty(profile.Groups);
        var ownerProfile = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        Assert.False(ownerProfile.IsGlobalAdministrator);
        var name = "Platform overview " + Guid.NewGuid().ToString("N");
        Guid groupId;
        await using (var database = factory.Database.CreateContext())
        {
            var group = new FantasyGroup { Name = name };
            database.GroupMemberships.Add(new() { Group = group, UserId = ownerProfile.Id, Roles = GroupRole.SuperAdmin | GroupRole.Admin });
            database.Users.Add(new() { Email = "disabled-" + name.Replace(" ", "").ToLowerInvariant() + "@example.test", DisplayName = name, IsEnabled = false });
            await database.SaveChangesAsync(); groupId = group.Id;
        }
        var groups = (await platform.GetFromJsonAsync<PlatformPage<PlatformGroupDto>>("/api/platform/groups?limit=1&query=" + Uri.EscapeDataString(name)))!;
        Assert.Equal(1, groups.Total); Assert.Equal(groupId, Assert.Single(groups.Items).Id);
        Assert.Equal(1, groups.Items[0].Members);
        var detail = (await platform.GetFromJsonAsync<PlatformGroupDetailsDto>($"/api/platform/groups/{groupId}"))!;
        Assert.Equal(ownerProfile.Id, Assert.Single(detail.Members).UserId);
        Assert.Empty((await platform.GetFromJsonAsync<GroupSummaryDto[]>("/api/workspace/groups"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync($"/api/workspace/groups/{groupId}")).StatusCode);
        var users = (await platform.GetFromJsonAsync<PlatformPage<PlatformUserDto>>("/api/platform/users?query=" + Uri.EscapeDataString(name)))!;
        Assert.False(Assert.Single(users.Items).IsEnabled);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.GetAsync("/api/platform/users?offset=-1")).StatusCode);
        Assert.Empty((await platform.GetFromJsonAsync<PlatformPage<PlatformGroupDto>>("/api/platform/groups?offset=1&limit=1&query=" + Uri.EscapeDataString(name)))!.Items);
        foreach (var client in new[] { owner, stranger })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/groups")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest("Forged"))).StatusCode);
        }
        // A signed/stale/forged role alone is never sufficient: the current
        // trusted administrator email list and enabled account are rechecked.
        stranger.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/platform/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest("Forged"))).StatusCode);
    }

    [PostgreSqlFact]
    public async Task One_shot_code_creates_one_group_atomically_even_with_concurrent_redemptions_and_is_never_listed_in_plaintext()
    {
        using var platform = await factory.AccountAsync(globalAdmin: true);
        using var first = await factory.AccountAsync(); using var second = await factory.AccountAsync();
        var label = "One shot " + Guid.NewGuid().ToString("N");
        var response = await platform.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest(label));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        var issued = (await response.Content.ReadFromJsonAsync<IssuedGroupCodeDto>())!;
        Assert.StartsWith("FZ-", issued.Code); Assert.Equal("Available", issued.Details.Status);
        var name = "Redeemed " + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest(" ", issued.Code))).StatusCode);
        var attempts = await Task.WhenAll(first.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest(name, issued.Code.ToLowerInvariant())),
            second.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest(name, issued.Code)));
        var success = Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        var created = (await success.Content.ReadFromJsonAsync<GroupSummaryDto>())!;
        await using var database = factory.Database.CreateContext();
        var stored = await database.GroupCreationCodes.SingleAsync(x => x.Id == issued.Details.Id);
        Assert.NotEqual(issued.Code, stored.CodeHash); Assert.Equal(64, stored.CodeHash.Length);
        Assert.Equal(created.GroupId, stored.RedeemedGroupId); Assert.NotNull(stored.RedeemedByUserId);
        Assert.NotNull(stored.RedeemedAtUtc);
        Assert.Single(await database.Groups.Where(x => x.Name == name).ToArrayAsync());
        var membership = await database.GroupMemberships.SingleAsync(x => x.GroupId == created.GroupId);
        Assert.Equal(stored.RedeemedByUserId, membership.UserId);
        Assert.Equal(GroupRole.SuperAdmin | GroupRole.Admin, membership.Roles);
        Assert.Equal(created.GroupId, (await database.AccountSelections.SingleAsync(x => x.UserId == membership.UserId)).GroupId);
        var codePage = await platform.GetStringAsync("/api/platform/group-codes");
        Assert.DoesNotContain(issued.Code, codePage); Assert.DoesNotContain(stored.CodeHash, codePage);
        Assert.Equal(HttpStatusCode.Conflict, (await first.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Reuse", issued.Code))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsync($"/api/platform/group-codes/{issued.Details.Id}/revoke", null)).StatusCode);
        var winner = attempts[0].IsSuccessStatusCode ? first : second;
        Assert.False((await winner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!.IsGlobalAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await winner.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("No bypass"))).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Codes_can_create_additional_groups_but_revoked_unknown_and_disabled_account_redemptions_do_not_consume_or_create()
    {
        using var platform = await factory.AccountAsync(globalAdmin: true); using var user = await factory.AccountAsync();
        async Task<IssuedGroupCodeDto> Issue() => (await (await platform.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest("Extra group")))
            .Content.ReadFromJsonAsync<IssuedGroupCodeDto>())!;
        var firstCode = await Issue(); var secondCode = await Issue();
        foreach (var code in new[] { firstCode, secondCode })
            (await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Additional group", code.Code))).EnsureSuccessStatusCode();
        Assert.Equal(2, (await user.GetFromJsonAsync<GroupSummaryDto[]>("/api/workspace/groups"))!.Length);
        var revoked = await Issue();
        (await platform.PostAsync($"/api/platform/group-codes/{revoked.Details.Id}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Revoked", revoked.Code))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Unknown", "FZ-" + new string('0', 48)))).StatusCode);
        var available = await Issue();
        var profile = (await user.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        await using (var database = factory.Database.CreateContext())
        { (await database.Users.SingleAsync(x => x.Id == profile.Id)).IsEnabled = false; await database.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Disabled", available.Code))).StatusCode);
        await using var final = factory.Database.CreateContext();
        Assert.Null((await final.GroupCreationCodes.SingleAsync(x => x.Id == available.Details.Id)).RedeemedAtUtc);
        Assert.Empty(await final.Groups.Where(x => x.Name == "Disabled" || x.Name == "Revoked").ToArrayAsync());
    }
}
