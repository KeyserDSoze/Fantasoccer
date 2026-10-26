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
                    new() { Ordinal = 1, OwnerUserId = second.Id, OwnerEmail = second.Email, Name = "Historic opponent", Points = 500 }],
                RecordGame = new() { Year = year, Id = Guid.NewGuid(), LegacyGameId = "standalone", Home = "Historical home", Away = "Historical away", Result = new() { Home = new() { Value = 100 }, Away = new() { Value = 90 } } } });
            database.FantasyCalendars.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stages = [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", Days =
                [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", SerieADay = 38, FantasyTurn = 1, Games =
                    [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Id = Guid.NewGuid(), Stage = "@", HomeBasketId = basket, HomeTeamId = ids[0], AwayBasketId = basket, AwayTeamId = ids[1],
                        HomeName = "Hall home", HomeOwner = first.Email, AwayName = "Hall away", AwayOwner = second.Email, Result = new() { Home = new() { Value = 71.5f }, Away = new() { Value = 66 }, HomeGoals = 2, AwayGoals = 1 } }] }] }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/leagues/{league}/hall-of-fame";
        Assert.Equal(HttpStatusCode.Forbidden, (await opponent.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/refresh", new RefreshHallRequest(1))).StatusCode);
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
}
