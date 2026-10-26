using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Fantasoccer;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Application.Api.Controllers;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rystem.Authentication.Social;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task SQL_invitation_can_log_in_without_a_legacy_identity_and_display_name_does_not_grant_privileges()
    {
        using var creator = await factory.AccountAsync(globalAdmin: true);
        var group = await CreateGroupAsync(creator);
        var incoming = await creator.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}");
        var email = $"invited-{Guid.NewGuid():N}@example.test";
        incoming = incoming! with { Users = [.. incoming!.Users, new("New friend", email, 2)] };
        (await creator.PutAsJsonAsync($"/api/groups/{group.GroupId}", incoming)).EnsureSuccessStatusCode();
        using var invited = factory.CreateClient();
        invited.DefaultRequestHeaders.Add("X-Workspace-User", email);
        var profile = await invited.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me");
        Assert.Equal(group.GroupId, Assert.Single(profile!.Groups).Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var social = scope.ServiceProvider.GetRequiredService<ISocialUserProvider>();
        var claims = new List<Claim>();
        await foreach (var claim in social.GetClaimsAsync(new() { Username = email.ToUpperInvariant(), Claims = [] }, default)) claims.Add(claim);
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Email && claim.Value == email);
        Assert.DoesNotContain(claims, claim => claim.Type == ClaimTypes.Role);
        (await invited.PatchAsJsonAsync("/api/accounts/me/profile", new { username = "alessandro.rapiti44@gmail.com", roles = "super_admin" })).EnsureSuccessStatusCode();
        claims.Clear();
        await foreach (var claim in social.GetClaimsAsync(new() { Username = email, Claims = [] }, default)) claims.Add(claim);
        Assert.DoesNotContain(claims, claim => claim.Type == ClaimTypes.Role);
        var response = await invited.GetAsync("/api/Authentication/Social/User");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("username", out _));
        Assert.True(json.TryGetProperty("groups", out _));
        Assert.False(json.TryGetProperty("u", out _));
    }

    [PostgreSqlFact]
    public async Task Verified_Microsoft_subject_binding_survives_alias_changes_and_rejects_email_reassignment()
    {
        using var client = await factory.AccountAsync();
        var profile = (await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        const string issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
        var subject = "subject-" + Guid.NewGuid().ToString("N");
        var first = await service.ResolveExternalLoginAsync(profile.Email, issuer, "fixture-audience", subject);
        Assert.Equal(profile.Id, first!.Id);
        var alias = await service.ResolveExternalLoginAsync("new-alias@example.test", issuer, "fixture-audience", subject);
        Assert.Equal(profile.Id, alias!.Id);
        Assert.Null(await service.ResolveExternalLoginAsync(profile.Email, issuer, "fixture-audience", "different-subject"));
    }

    [PostgreSqlFact]
    public async Task Selections_are_private_and_cannot_reference_another_group_or_a_wrong_season()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var first = await CreateGroupAsync(owner);
        var second = await CreateGroupAsync(owner);
        var firstLeague = await CreateLeagueAsync(owner, first.GroupId, 15);
        var secondLeague = await CreateLeagueAsync(owner, second.GroupId, 16);
        var account = await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/accounts/me/selection", new AccountStateDto(first.GroupId, secondLeague, 16))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/accounts/me/selection", new AccountStateDto(first.GroupId, firstLeague, 14))).StatusCode);
        (await owner.PutAsJsonAsync("/api/accounts/me/selection", new AccountStateDto(first.GroupId, firstLeague, 15, account!.Email))).EnsureSuccessStatusCode();
        var updated = await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me");
        Assert.Equal(firstLeague, updated!.State.LeagueId);
        Assert.Equal(15, updated.State.Year);
        using var outsider = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PutAsJsonAsync("/api/accounts/me/selection", updated.State)).StatusCode);
        Assert.Empty((await outsider.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!.Groups);
    }

    [PostgreSqlFact]
    public async Task Preference_updates_are_whitelisted_and_do_not_overwrite_private_prompt_or_memberships()
    {
        using var client = await factory.AccountAsync();
        var account = await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me");
        await using (var database = factory.Database.CreateContext())
        {
            database.AccountPreferences.Add(new() { UserId = account!.Id, AiPrompt = "Keep private prompt", LiveWidgetEnabled = true });
            await database.SaveChangesAsync();
        }
        (await client.PutAsJsonAsync("/api/accounts/me/settings", new { autoOptimalFormation = true, liveWidgetEnabled = false, aiPrompt = "Forged prompt", groups = new[] { "forged" } })).EnsureSuccessStatusCode();
        var settings = await client.GetFromJsonAsync<UserSettingsDto>("/api/accounts/me/settings");
        Assert.Equal("Keep private prompt", settings!.AiPrompt);
        Assert.True(settings.AutoOptimalFormation);
        Assert.False(settings.LiveWidgetEnabled);
        Assert.Empty((await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!.Groups);
    }

    [PostgreSqlFact]
    public async Task Complete_group_DTO_preserves_rules_team_identity_and_ownership_history_and_rejects_stale_updates()
    {
        using var client = await factory.AccountAsync(globalAdmin: true);
        var group = await CreateGroupAsync(client);
        await CreateLeagueAsync(client, group.GroupId, 15);
        var path = $"/api/groups/{group.GroupId}";
        var original = (await client.GetFromJsonAsync<GameGroupDto>(path))!;
        var email = $"coowner-{Guid.NewGuid():N}@example.test";
        original = original with { Users = [.. original.Users, new("Co-owner", email, 2)] };
        var settings = original.Leagues[0].Years[0].Settings;
        settings.Votes[-1] = settings.Votes[-1] with { Assist = 1.5f, Injury = -1 };
        settings.Formation = 1;
        settings.OpeningCompetition = new(true, 3, [new(1, 70), new(2, 20)]);
        settings.TypeSettings = new(0, [new("Final round", 1, true, 1, 4)], new(25, 1, 5, 4, 2, 1, 1, 1, 1), null, new(new() { [0] = 2 }));
        var owner = await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me");
        original = original with { Baskets = [original.Baskets[0] with { Years = [new(15, [new("Team", owner!.Email, [email])])] }] };
        var firstSave = await client.PutAsJsonAsync(path, original);
        firstSave.EnsureSuccessStatusCode();
        var saved = (await firstSave.Content.ReadFromJsonAsync<GameGroupDto>())!;
        var teamId = saved.Baskets[0].Years[0].Teams[0].TeamId;
        var transfer = saved with { Baskets = [saved.Baskets[0] with { Years = [new(15, [new("Renamed team", email, [owner.Email], teamId)])] }] };
        var transferResponse = await client.PutAsJsonAsync(path, transfer);
        transferResponse.EnsureSuccessStatusCode();
        var final = (await transferResponse.Content.ReadFromJsonAsync<GameGroupDto>())!;
        Assert.Equal(teamId, final.Baskets[0].Years[0].Teams[0].TeamId);
        Assert.Equal(email, final.Baskets[0].Years[0].Teams[0].Owner);
        Assert.Equal(1.5f, final.Leagues[0].Years[0].Settings.Votes[-1].Assist);
        Assert.Equal("Final round", final.Leagues[0].Years[0].Settings.TypeSettings!.Rounds[0].Name);
        Assert.Equal(70, final.Leagues[0].Years[0].Settings.OpeningCompetition.Prizes[0].Credits);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, saved)).StatusCode);
        await using var database = factory.Database.CreateContext();
        Assert.True(await database.TeamOwnershipChanges.AnyAsync(x => x.GroupId == group.GroupId && x.TeamId == teamId && !x.Added));
    }

    [PostgreSqlFact]
    public async Task Provisioning_across_groups_is_atomic_when_one_group_is_not_authorized()
    {
        using var firstOwner = await factory.AccountAsync(globalAdmin: true);
        using var secondOwner = await factory.AccountAsync(globalAdmin: true);
        var first = await CreateGroupAsync(firstOwner);
        var second = await CreateGroupAsync(secondOwner);
        var email = $"not-created-{Guid.NewGuid():N}@example.test";
        var result = await firstOwner.PostAsJsonAsync("/api/accounts", new ProvisionAccountRequest(email, "New friend",
            [new(first.GroupId, first.Name, 2), new(second.GroupId, second.Name, 2)]));
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        await using var database = factory.Database.CreateContext();
        Assert.False(await database.Users.AnyAsync(x => x.Email == email));
    }

    [PostgreSqlFact]
    public async Task Team_logos_remain_files_with_versioned_references_and_nonowners_cannot_replace_them()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var other = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner);
        await CreateLeagueAsync(owner, group.GroupId, 15);
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var profile = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var outsider = (await other.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(outsider.Email, "Other", 2))).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, 15, "Logo team", profile.Id, []))).EnsureSuccessStatusCode();
        var query = $"groupId={group.GroupId}&basketId={graph.Baskets[0].Id}&year=15&owner={Uri.EscapeDataString(profile.Email)}";
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jC9sAAAAASUVORK5CYII=");
        MultipartFormDataContent Body()
        {
            var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(image), "image", "logo.png"); return content;
        }
        using var firstBody = Body();
        var first = await owner.PostAsync("/api/team-assets/logo?" + query, firstBody);
        first.EnsureSuccessStatusCode();
        var logo = (await first.Content.ReadFromJsonAsync<TeamAssetResult>())!;
        Assert.Contains("/logos/", logo.LogoUrl);
        using var forbiddenBody = Body();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync("/api/team-assets/logo?" + query, forbiddenBody)).StatusCode);
        using var replacement = Body();
        var second = await owner.PostAsync("/api/team-assets/logo?" + query, replacement);
        second.EnsureSuccessStatusCode();
        var updated = (await second.Content.ReadFromJsonAsync<TeamAssetResult>())!;
        Assert.NotEqual(logo.LogoUrl, updated.LogoUrl);
        var final = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        Assert.Equal(updated.LogoUrl, final.Baskets[0].Years[0].Teams[0].LogoUrl);
        (await owner.PatchAsJsonAsync("/api/team-assets/name", new TeamAssetsController.RenameTeamRequest(group.GroupId, graph.Baskets[0].Id, 15, profile.Email, "New logo team"))).EnsureSuccessStatusCode();
        var renamed = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        Assert.Equal("New logo team", renamed.Baskets[0].Years[0].Teams[0].Name);
    }

    [PostgreSqlFact]
    public async Task Saving_a_daily_formation_uses_server_roster_prices_and_rejects_foreign_players_and_stale_versions()
    {
        using var client = await factory.AccountAsync(globalAdmin: true);
        var group = await CreateGroupAsync(client);
        var league = await CreateLeagueAsync(client, group.GroupId, 17);
        var graph = (await client.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var account = (await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var basket = graph.Baskets[0].Id;
        var created = await client.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, 17, "Formation team", account.Id, []));
        created.EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season);
            var club = new FootballClub { Name = "Club", Abbreviation = "clb" };
            var away = new FootballClub { Name = "Away", Abbreviation = "awy" };
            database.Clubs.AddRange(club, away);
            var roles = new[] { 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 1 };
            for (var index = 0; index < roles.Length; index++)
            {
                var person = new FootballPlayer { Name = "Formation player " + Guid.NewGuid().ToString("N") };
                database.Players.Add(person);
                database.PlayerSeasons.Add(new() { Player = person, SeasonIndex = 17, Club = club, Position = (PlayerPosition)roles[index], IsActive = true });
                roster.Players.Add(new() { Ordinal = index, Player = person, Club = club, PlayerRole = roles[index], IsActive = true,
                    IsVisible = true, Price = 10 + index, Status = 0, Position = index == 11 ? 5 : roles[index] });
            }
            roster.Players.Add(new() { Ordinal = 99, Player = roster.Players.First().Player, Club = club, PlayerRole = 0,
                Price = 7, Revenue = 12, Status = 3, Position = 11 });
            roster.MoneyFromRank = 56; roster.OpeningCompetitionPrize = 11;
            database.FootballMatchdays.Add(new() { SeasonIndex = 17, SerieADay = 1, Matches =
                [new() { SeasonIndex = 17, SerieADay = 1, Ordinal = 0, HomeClub = club, AwayClub = away, KickoffUtc = DateTimeOffset.UtcNow.AddDays(1) }] });
            await database.SaveChangesAsync();
        }
        var query = $"groupId={group.GroupId}&basketId={basket}&year=17&owner={Uri.EscapeDataString(account.Email)}";
        var baseline = (await client.GetFromJsonAsync<TeamRosterDto>("/api/teams/roster?" + query + "&kind=0&day=0"))!;
        var positions = baseline.Players.Where(x => x.Status == 0).Select(x => new FormationPositionRequest(x.PlayerId, x.Position)).ToArray();
        positions[1] = positions[1] with { Position = 5 };
        positions[11] = positions[11] with { Position = 1 };
        var request = new SaveRosterFormationRequest(group.GroupId, basket, 17, account.Email, 1, league, 1, positions, 0, SourceVersion: baseline.Version);
        var response = await client.PutAsJsonAsync("/api/teams/formation", new
        {
            request.GroupId, request.BasketId, request.Year, request.Owner, request.Day, request.LeagueId, request.Kind,
            request.Positions, request.ExpectedVersion, request.Live, request.AsAdmin,
            request.SourceVersion,
            moneyFromRank = 99999, openingCompetitionPrize = 99999, prices = new[] { 0 }
        });
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<FormationSavedDto>())!;
        Assert.True(saved.IsOk);
        Assert.Equal(56, saved.Team.MoneyFromRank);
        Assert.Equal(11, saved.Team.OpeningCompetitionPrize);
        Assert.Equal(7, saved.Team.Players.Single(x => x.Status == 3).Price);
        Assert.Equal(baseline.Players.Select(x => x.Price), saved.Team.Players.Select(x => x.Price));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/teams/formation", request)).StatusCode);
        var forged = request with { ExpectedVersion = saved.Team.Version, Positions = [new(Guid.NewGuid(), 0), .. positions.Skip(1)] };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/teams/formation", forged)).StatusCode);
        using var outsider = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PutAsJsonAsync("/api/teams/formation", request)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task OAuth_SQL_storage_supports_atomic_compare_and_swap_and_retention()
    {
        var store = factory.Services.GetRequiredService<IOAuthDocumentStorage>();
        var id = Guid.NewGuid().ToString("N");
        var kind = "test-" + id;
        Assert.True(await store.WriteAsync(new() { Id = id, Kind = kind, Version = "first", Payload = "{}" }, null));
        Assert.False(await store.WriteAsync(new() { Id = id, Kind = kind, Version = "duplicate", Payload = "{}" }, null));
        var results = await Task.WhenAll(store.WriteAsync(new() { Id = id, Kind = kind, Version = "second", Payload = "{}" }, "first"),
            store.WriteAsync(new() { Id = id, Kind = kind, Version = "third", Payload = "{}" }, "first"));
        Assert.Equal(1, results.Count(value => value));
        Assert.NotEqual("first", (await store.GetAsync(id))!.Version);
        var expired = id + "-expired";
        await store.WriteAsync(new() { Id = expired, Kind = kind, Version = "first", Payload = "{}", DeleteAfterUtc = DateTimeOffset.UtcNow.AddDays(-1) }, null);
        Assert.Equal(1, await store.PruneAsync(DateTimeOffset.UtcNow));
        Assert.Null(await store.GetAsync(expired));
        Assert.NotNull(await store.GetAsync(id));
    }

    private static async Task<GroupSummaryDto> CreateGroupAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Friends " + Guid.NewGuid().ToString("N")));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupSummaryDto>())!;
    }
    private static async Task<Guid> CreateLeagueAsync(HttpClient client, Guid groupId, int year)
    {
        var path = $"/api/workspace/groups/{groupId}";
        var basketResponse = await client.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Basket", year));
        basketResponse.EnsureSuccessStatusCode();
        var basket = (await basketResponse.Content.ReadFromJsonAsync<BasketCreatedDto>())!;
        var leagueResponse = await client.PostAsJsonAsync(path + "/leagues", new CreateLeagueRequest("League", year, [basket.BasketId]));
        leagueResponse.EnsureSuccessStatusCode();
        return (await leagueResponse.Content.ReadFromJsonAsync<LeagueCreatedDto>())!.LeagueId;
    }
}
