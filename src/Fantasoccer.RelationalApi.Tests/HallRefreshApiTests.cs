using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.HallOfFame;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Hall_refresh_requires_reconciled_baseline_preserves_missing_history_and_applies_only_new_season_deltas_once()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); using var opponent = await factory.AccountAsync();
        var first = (await admin.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!; var second = (await opponent.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(admin); var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(admin, group.GroupId, year);
        (await admin.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(second.Email, "Opponent", 2))).EnsureSuccessStatusCode();
        var graph = (await admin.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!; var basket = graph.Baskets[0].Id;
        var ids = new List<Guid>();
        foreach (var account in new[] { first, second })
        {
            var response = await admin.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, account.Id == first.Id ? "Hall home" : "Hall away", account.Id, []));
            response.EnsureSuccessStatusCode(); ids.Add((await response.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId);
        }
        await using (var database = factory.Database.CreateContext())
        {
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 38,
                Stages = [new() { Stage = "@", Teams = [new() { Ordinal = 0, BasketId = basket, TeamId = ids[0], Name = "Hall home", Owner = first.Email, Points = 10 },
                    new() { Ordinal = 1, BasketId = basket, TeamId = ids[1], Name = "Hall away", Owner = second.Email, Points = 3 }] }] });
            database.HallArchives.Add(new() { GroupId = group.GroupId, LeagueId = league, ContributionsInitialized = false,
                Ranks = [new() { Ordinal = 0, OwnerUserId = first.Id, OwnerEmail = first.Email, Name = "Historic identity", Points = 1000.5f },
                    new() { Ordinal = 1, OwnerUserId = second.Id, OwnerEmail = second.Email, Name = "Historic opponent", Points = 2 }],
                RecordGame = new() { Year = year, Id = Guid.NewGuid(), LegacyGameId = "standalone", Home = "Historical home", Away = "Historical away", Result = new() { Home = new() { Value = 100 }, Away = new() { Value = 90 } } } });
            database.FantasyCalendars.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stages = [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", Days =
                [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", SerieADay = 38, FantasyTurn = 1, Games =
                    [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Id = Guid.NewGuid(), Stage = "@", HomeBasketId = basket, HomeTeamId = ids[0], AwayBasketId = basket, AwayTeamId = ids[1],
                        HomeName = "Hall home", HomeOwner = first.Email, AwayName = "Hall away", AwayOwner = second.Email, Result = new() { Home = new() { Value = 71.5f }, Away = new() { Value = 66 }, HomeGoals = 2, AwayGoals = 1 } }] }] }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/leagues/{league}/hall-of-fame";
        Assert.Equal(HttpStatusCode.Forbidden, (await opponent.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1))).StatusCode);
        // The opponent's career total (2) is below the 3 points of the season we have: the totals do not contain it, an administrator decides.
        var blocked = await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("punti 2 nei totali, 3 nelle stagioni", await blocked.Content.ReadAsStringAsync());
        (await admin.PostAsJsonAsync(path + "/confirm-baseline", new RefreshHallRequest(1))).EnsureSuccessStatusCode();
        var sealedArchive = (await admin.GetFromJsonAsync<HallOfFameDto>(path))!;
        Assert.Equal(1000.5f, sealedArchive.AllTimeRankings.Single(x => x.OwnerUserId == first.Id).Points);
        Assert.True(sealedArchive.BaselineConfirmed); Assert.False(sealedArchive.RecordGame!.IsLinkedToCalendar);
        await using (var database = factory.Database.CreateContext())
        {
            var snapshot = await database.FantasyStandingSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league && x.SnapshotDay == 0);
            snapshot.Stages.Single().Teams.Single(x => x.TeamId == ids[0]).Points = 13;
            await database.SaveChangesAsync();
        }
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(sealedArchive.Version))).EnsureSuccessStatusCode();
        var refreshed = (await admin.GetFromJsonAsync<HallOfFameDto>(path))!;
        Assert.Equal(1003.5f, refreshed.AllTimeRankings.Single(x => x.OwnerUserId == first.Id).Points);
        Assert.False(refreshed.RecordGame!.IsLinkedToCalendar); // A smaller SQL game never erases the standalone record.
        Assert.Equal(year, refreshed.WinningTeams.Single(x => x.OwnerUserId == first.Id).Wins["@"].Single());
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(refreshed.Version))).EnsureSuccessStatusCode();
        Assert.Equal(refreshed.Version, (await admin.GetFromJsonAsync<HallOfFameDto>(path))!.Version);
    }
    [PostgreSqlFact]
    public async Task Hall_refresh_seals_the_baseline_by_itself_when_the_career_totals_contain_the_available_seasons_and_resolves_owners_who_left()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); using var outsider = await factory.AccountAsync();
        var first = (await admin.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!; var gone = (await outsider.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(admin); var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(admin, group.GroupId, year);
        var graph = (await admin.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!; var basket = graph.Baskets[0].Id;
        var response = await admin.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Hall auto home", first.Id, []));
        response.EnsureSuccessStatusCode(); var team = (await response.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            // The totals hold older years (and a league born later has none): 1000.5 contains the 10 of the season we have. The owner who left
            // the group (never a member here) has no career row: his season is added, never replaced.
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 38,
                Stages = [new() { Stage = "@", Teams = [new() { Ordinal = 0, BasketId = basket, TeamId = team, Name = "Hall auto home", Owner = first.Email, Points = 10, Victories = 3 },
                    new() { Ordinal = 1, BasketId = basket, TeamId = team, Name = "Left the group", Owner = gone.Email, Points = 4, Victories = 1 }] }] });
            database.HallArchives.Add(new() { GroupId = group.GroupId, LeagueId = league, ContributionsInitialized = false,
                Ranks = [new() { Ordinal = 0, OwnerUserId = first.Id, OwnerEmail = first.Email, Name = "Historic identity", Points = 1000.5f, Victories = 120 }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/leagues/{league}/hall-of-fame";
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1))).EnsureSuccessStatusCode();
        var hall = (await admin.GetFromJsonAsync<HallOfFameDto>(path))!;
        Assert.True(hall.BaselineConfirmed);
        Assert.Equal(1000.5f, hall.AllTimeRankings.Single(x => x.OwnerUserId == first.Id).Points); // already counted: not added twice
        Assert.Equal(4, hall.AllTimeRankings.Single(x => x.OwnerUserId == gone.Id).Points);
        await using (var database = factory.Database.CreateContext())
        {
            Assert.Null((await database.HallBaselineConfirmations.SingleAsync(x => x.GroupId == group.GroupId)).ActorUserId);
            Assert.Equal(1, await database.HallRankContributions.CountAsync(x => x.GroupId == group.GroupId && x.OwnerUserId == first.Id));
        }
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(hall.Version))).EnsureSuccessStatusCode();
        Assert.Equal(hall.Version, (await admin.GetFromJsonAsync<HallOfFameDto>(path))!.Version);
        Assert.Equal(4, (await admin.GetFromJsonAsync<HallOfFameDto>(path))!.AllTimeRankings.Single(x => x.OwnerUserId == gone.Id).Points);
    }
    [PostgreSqlFact]
    public async Task Hall_refresh_counts_only_the_rest_of_the_season_that_was_in_progress_at_the_import_and_merges_rows_split_for_the_same_person()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var first = (await admin.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(admin); var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(admin, group.GroupId, year);
        var graph = (await admin.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!; var basket = graph.Baskets[0].Id;
        var response = await admin.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "In progress", first.Id, []));
        response.EnsureSuccessStatusCode(); var team = (await response.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            foreach (var past in new[] { year - 1, year - 2 })
                if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == past)) database.Seasons.Add(new() { SeasonIndex = past, StartYear = past + 2011 });
            // Now 10 points and 3 wins; at the import the old Hall of Fame had counted 6 and 1 of them, split on two rows for the same person.
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 10,
                Stages = [new() { Stage = "@", Teams = [new() { Ordinal = 0, BasketId = basket, TeamId = team, Name = "In progress", Owner = first.Email, Points = 10, Victories = 3 }] }] });
            database.HallArchives.Add(new() { GroupId = group.GroupId, LeagueId = league, ContributionsInitialized = false,
                Ranks = [new() { Ordinal = 0, OwnerUserId = first.Id, OwnerEmail = first.Email, Name = "In progress", Points = 4, Victories = 1 },
                    new() { Ordinal = 1, OwnerUserId = first.Id, OwnerEmail = first.Email.ToUpperInvariant(), Name = "In progress", Points = 2 }],
                WinningTeams = [new() { Ordinal = 0, OwnerUserId = first.Id, OwnerEmail = first.Email, Name = "A", Wins = [new() { Stage = "@", Years = [new() { Year = year - 2 }] }] },
                    new() { Ordinal = 1, OwnerUserId = first.Id, OwnerEmail = first.Email, Name = "B", Wins = [new() { Stage = "@", Years = [new() { Year = year - 1 }] }] }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/leagues/{league}/hall-of-fame";
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1))).EnsureSuccessStatusCode();
        var hall = (await admin.GetFromJsonAsync<HallOfFameDto>(path))!;
        var career = Assert.Single(hall.AllTimeRankings, x => x.OwnerUserId == first.Id);
        Assert.Equal(10, career.Points); Assert.Equal(3, career.Victories); // 6 already counted + the 4 played after the import
        Assert.Equal(new[] { year - 2, year - 1 }, Assert.Single(hall.WinningTeams, x => x.OwnerUserId == first.Id).Wins["@"].Order());
        (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(hall.Version))).EnsureSuccessStatusCode();
        Assert.Equal(10, (await admin.GetFromJsonAsync<HallOfFameDto>(path))!.AllTimeRankings.Single(x => x.OwnerUserId == first.Id).Points);
    }
}
