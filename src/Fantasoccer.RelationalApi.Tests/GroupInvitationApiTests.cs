using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Code_created_group_owner_can_lookup_exact_email_and_invite_without_access_to_the_platform_directory()
    {
        using var platform = await factory.AccountAsync(globalAdmin: true);
        using var owner = await factory.AccountAsync(); using var friend = await factory.AccountAsync(); using var stranger = await factory.AccountAsync();
        var friendProfile = (await friend.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var otherGroup = await CreateGroupAsync(platform);
        (await platform.PutAsJsonAsync($"/api/workspace/groups/{otherGroup.GroupId}/members", new SetGroupMemberRequest(friendProfile.Email, "Other member", 2))).EnsureSuccessStatusCode();
        var issued = (await (await platform.PostAsJsonAsync("/api/platform/group-codes", new IssueGroupCodeRequest("Invitation test")))
            .Content.ReadFromJsonAsync<IssuedGroupCodeDto>())!;
        var response = await owner.PostAsJsonAsync("/api/workspace/groups/redeem-code", new RedeemGroupCodeRequest("Owner invitation", issued.Code));
        response.EnsureSuccessStatusCode(); var group = (await response.Content.ReadFromJsonAsync<GroupSummaryDto>())!;
        Assert.True((group.Roles & 4) != 0);
        var path = $"/api/workspace/groups/{group.GroupId}/members";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/platform/users")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(path + "/lookup?email=friend")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(path + "/lookup?email=" + Uri.EscapeDataString(friendProfile.Email))).StatusCode);
        var lookup = (await owner.GetFromJsonAsync<JsonElement>(path + "/lookup?email=" + Uri.EscapeDataString(friendProfile.Email.ToUpperInvariant())))!;
        Assert.True(lookup.GetProperty("found").GetBoolean()); Assert.False(lookup.GetProperty("isMember").GetBoolean());
        Assert.Equal(4, lookup.EnumerateObject().Count());
        Assert.False(lookup.TryGetProperty("groups", out _)); Assert.False(lookup.TryGetProperty("state", out _));
        Assert.DoesNotContain(otherGroup.GroupId.ToString(), lookup.GetRawText());
        var exact = (await owner.GetFromJsonAsync<AccountIdentityDto[]>("/api/accounts?email=" + Uri.EscapeDataString(friendProfile.Email)))!;
        Assert.Empty(Assert.Single(exact).Groups);
        (await owner.PutAsJsonAsync(path, new SetGroupMemberRequest(friendProfile.Email, "Friend", 4))).EnsureSuccessStatusCode();
        // An ordinary group administrator (not owner/platform admin) can add
        // participants, but cannot grant the group-owner bit.
        var strangerProfile = (await stranger.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await friend.GetAsync(path + "/lookup?email=" + Uri.EscapeDataString(strangerProfile.Email))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await friend.PutAsJsonAsync(path, new SetGroupMemberRequest(strangerProfile.Email, "Stranger", 8))).StatusCode);
        (await friend.PutAsJsonAsync(path, new SetGroupMemberRequest(strangerProfile.Email, "Stranger", 2))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(path + "/lookup?email=" + Uri.EscapeDataString(friendProfile.Email))).StatusCode);
        var platformUsers = (await platform.GetFromJsonAsync<PlatformPage<PlatformUserDto>>("/api/platform/users?query=" + Uri.EscapeDataString(friendProfile.Email)))!;
        Assert.Equal(2, Assert.Single(platformUsers.Items).Groups.Length);
    }

    [PostgreSqlFact]
    public async Task Invitation_can_preassociate_an_unregistered_email_and_verified_first_login_gets_only_that_group()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var group = await CreateGroupAsync(owner); var email = $"not-registered-{Guid.NewGuid():N}@example.test";
        var path = $"/api/workspace/groups/{group.GroupId}/members";
        var result = (await owner.GetFromJsonAsync<GroupMemberLookupDto>(path + "/lookup?email=" + Uri.EscapeDataString(email)))!;
        Assert.False(result.Found); Assert.Null(result.DisplayName);
        (await owner.PutAsJsonAsync(path, new SetGroupMemberRequest(email, "Invited friend", 2))).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<AccountApplicationService>().ResolveExternalLoginAsync(email,
            "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0", "fixture", Guid.NewGuid().ToString("N"));
        Assert.NotNull(account); Assert.Equal(group.GroupId, Assert.Single(account.Groups).Id); Assert.False(account.IsGlobalAdministrator);
    }
}
