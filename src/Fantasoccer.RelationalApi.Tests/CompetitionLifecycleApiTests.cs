using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Calendar_generation_and_explicit_recalculation_are_atomic_versioned_and_reuse_SQL_official_votes()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var participant = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, 20);
        var owner = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var guest = (await participant.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(guest.Email, "Guest", 2))).EnsureSuccessStatusCode();
        foreach (var user in new[] { owner, guest })
            (await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, 20, user.Id == owner.Id ? "First" : "Second", user.Id, []))).EnsureSuccessStatusCode();
        var path = $"/api/competitions/{group.GroupId}/{league}/20";
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync(path + "/calendar/generate", new GenerateCompetitionRequest())).StatusCode);
        (await manager.PostAsJsonAsync(path + "/calendar/generate", new GenerateCompetitionRequest())).EnsureSuccessStatusCode();
        var calendar = (await manager.GetFromJsonAsync<FantasyCalendarDto>(path + "/calendar"))!;
        Assert.NotEmpty(calendar.Rounds);
        Assert.Equal(1, calendar.Version);
        var day = calendar.Rounds.SelectMany(x => x.Value).First(x => x.SerieADay == 4);
        Assert.Equal(2, day.Number);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/calendar/generate", new GenerateCompetitionRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/recalculate", new RecalculateCompetitionRequest(4, PreciseDay: true))).StatusCode);
        await using (var database = factory.Database.CreateContext())
        {
            Assert.False(await database.PlayedMatchSnapshots.AnyAsync(x => x.GroupId == group.GroupId));
            var rosters = await database.RosterSnapshots.Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season).ToArrayAsync();
            var clubs = new[] { new FootballClub { Name = "Lifecycle club A", Abbreviation = "lca" }, new FootballClub { Name = "Lifecycle club B", Abbreviation = "lcb" } };
            database.Clubs.AddRange(clubs);
            var index = 0;
            foreach (var roster in rosters)
            {
                var club = clubs[index++];
                var player = new FootballPlayer { Name = "Lifecycle keeper " + Guid.NewGuid().ToString("N") };
                database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = 20, Position = PlayerPosition.Goalkeeper });
                database.FootballVotes.Add(new() { Player = player, Club = club, SeasonIndex = 20, Day = 4, Source = FootballVoteSource.Official, Value = 6,
                    PlayerRole = 0, VoteRole = 0, HasVote = true, IsFinal = true });
                database.RosterSnapshots.Add(new() { GroupId = group.GroupId, BasketId = basket, SeasonIndex = 20, TeamOriginYear = 20,
                    TeamId = roster.TeamId, Kind = RosterKind.Matchday, Day = 4, CompetitionId = Guid.Empty, Name = roster.Name, OwnerUserId = roster.OwnerUserId,
                    Players = [new() { Ordinal = 0, Player = player, Club = club, PlayerRole = 0, Position = 0, Status = 0, Price = 44 }] });
            }
            database.FootballMatchdays.Add(new() { SeasonIndex = 20, SerieADay = 4, Matches =
                [new() { SeasonIndex = 20, SerieADay = 4, Ordinal = 0, HomeClub = clubs[0], AwayClub = clubs[1], KickoffUtc = DateTimeOffset.UtcNow.AddDays(-1) }] });
            await database.SaveChangesAsync();
        }
        var request = new RecalculateCompetitionRequest(4, PreciseDay: true, ExpectedVersion: 1);
        (await manager.PostAsJsonAsync(path + "/recalculate", request)).EnsureSuccessStatusCode();
        var updated = (await manager.GetFromJsonAsync<FantasyCalendarDto>(path + "/calendar"))!;
        Assert.Equal(2, updated.Version);
        Assert.Equal(9, updated.Rounds.SelectMany(x => x.Value).First(x => x.SerieADay == 4).Games[0].Result!.Home.Value);
        var rank = (await manager.GetFromJsonAsync<FantasyRankDto>(path + "/rank"))!;
        var daily = (await manager.GetFromJsonAsync<FantasyRankDto>(path + "/rank?day=4"))!;
        Assert.Equal(4, rank.SerieADay);
        Assert.Equal(4, daily.SnapshotDay);
        Assert.All(rank.Rounds.SelectMany(x => x.Value), x => Assert.Equal(1, x.Draws));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/recalculate", request)).StatusCode);
        (await manager.PostAsJsonAsync(path + "/recalculate", request with { ExpectedVersion = 2 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/calendar/generate", new GenerateCompetitionRequest(3, true))).StatusCode);
        await using var final = factory.Database.CreateContext();
        Assert.Equal(1, await final.PlayedMatchSnapshots.CountAsync(x => x.GroupId == group.GroupId));
        Assert.All((await final.PlayedMatchSnapshots.SingleAsync(x => x.GroupId == group.GroupId)).Players, x => Assert.Equal(44, x.Price));
    }
}
