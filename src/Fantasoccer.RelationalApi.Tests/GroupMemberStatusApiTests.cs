using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Group_admin_can_manage_a_user_without_email_by_ID_and_link_a_verified_login_without_changing_history()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var admin = await factory.AccountAsync(); using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner); var other = await CreateGroupAsync(owner);
        var adminProfile = (await admin.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(adminProfile.Email, "Admin", 4))).EnsureSuccessStatusCode();
        var id = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            database.GroupMemberships.Add(new() { GroupId = group.GroupId, User = new() { Id = id, DisplayName = "Historical user", Email = null, IsEnabled = true }, Roles = 0 });
            await database.SaveChangesAsync();
        }
        var path = $"/api/workspace/groups/{group.GroupId}/members/{id}";
        var initial = (await admin.GetFromJsonAsync<GroupDetailsDto>($"/api/workspace/groups/{group.GroupId}"))!.Members.Single(x => x.UserId == id);
        Assert.False(initial.IsActive); Assert.Null(initial.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PatchAsJsonAsync($"/api/workspace/groups/{other.GroupId}/members/{id}", new UpdateGroupMemberRequest(true, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 8))).StatusCode);
        (await admin.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2))).EnsureSuccessStatusCode();
        (await admin.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(false, 0))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            Assert.True((await database.Users.SingleAsync(x => x.Id == id)).IsEnabled);
            Assert.Equal(GroupRole.None, (await database.GroupMemberships.SingleAsync(x => x.GroupId == group.GroupId && x.UserId == id)).Roles);
            Assert.False(await database.ExternalLogins.AnyAsync(x => x.UserId == id));
        }
        var email = $"historical-{Guid.NewGuid():N}@example.test";
        (await admin.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2, email))).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        var profile = await accounts.ResolveExternalLoginAsync(email, "verified-fixture", "fixture", Guid.NewGuid().ToString("N"));
        Assert.NotNull(profile); Assert.Equal(id, profile.Id); Assert.Equal(2, Assert.Single(profile.Groups).Role);
        Assert.False(profile.IsGlobalAdministrator);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2, "different@example.test"))).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Group_deactivation_does_not_disable_the_global_account_or_other_groups_and_cannot_remove_the_last_manager()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true); using var user = await factory.AccountAsync();
        var first = await CreateGroupAsync(owner); var second = await CreateGroupAsync(owner);
        var profile = (await user.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var ownerProfile = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        foreach (var group in new[] { first, second })
            (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(profile.Email, "Friend", 2))).EnsureSuccessStatusCode();
        (await owner.PatchAsJsonAsync($"/api/workspace/groups/{first.GroupId}/members/{profile.Id}", new UpdateGroupMemberRequest(false, 0))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/workspace/groups/{first.GroupId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/workspace/groups/{second.GroupId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync($"/api/workspace/groups/{first.GroupId}/members/{ownerProfile.Id}", new UpdateGroupMemberRequest(false, 0))).StatusCode);
        (await owner.PatchAsJsonAsync($"/api/workspace/groups/{first.GroupId}/members/{profile.Id}", new UpdateGroupMemberRequest(true, 2))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/workspace/groups/{first.GroupId}")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Assigning_an_existing_email_never_merges_users_and_a_group_admin_cannot_enable_a_globally_disabled_account()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true); using var other = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner);
        var profile = (await other.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var id = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            database.GroupMemberships.Add(new() { GroupId = group.GroupId, User = new() { Id = id, DisplayName = "No email", IsEnabled = true }, Roles = 0 });
            await database.SaveChangesAsync();
        }
        var path = $"/api/workspace/groups/{group.GroupId}/members/{id}";
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2, profile.Email))).StatusCode);
        await using (var database = factory.Database.CreateContext())
        {
            var stored = await database.Users.SingleAsync(x => x.Id == id); Assert.Null(stored.Email);
            Assert.False(await database.GroupMemberships.AnyAsync(x => x.GroupId == group.GroupId && x.UserId == profile.Id));
            stored.IsEnabled = false; await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(true, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync(path, new UpdateGroupMemberRequest(false, 0, "disabled-new@example.test"))).StatusCode);
        await using var final = factory.Database.CreateContext();
        Assert.False((await final.Users.SingleAsync(x => x.Id == id)).IsEnabled);
        Assert.Null((await final.Users.SingleAsync(x => x.Id == id)).Email);
        Assert.Equal(GroupRole.None, (await final.GroupMemberships.SingleAsync(x => x.GroupId == group.GroupId && x.UserId == id)).Roles);
    }
}
