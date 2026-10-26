using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Auctions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Rank_awards_are_checkpointed_and_never_overwrite_imported_balances_or_market_adjustments()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var year = FantacalcioTime.ActualYear;
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, year);
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var account = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var basket = graph.Baskets[0].Id;
        var created = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Credit team", account.Id, []));
        created.EnsureSuccessStatusCode();
        var team = (await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).IsMain = true;
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            roster.MoneyFromRank = 35; roster.MarketBalanceAdjustment = -10; roster.RankCreditGranted = null;
            var club = new FootballClub { Name = "Credit club", Abbreviation = "crc" };
            var player = new FootballPlayer { Name = "Credit available player " + Guid.NewGuid().ToString("N") };
            database.Clubs.Add(club); database.Players.Add(player);
            database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, Position = PlayerPosition.Forward });
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 3, Stages =
                [new() { Stage = "@", Teams = [new() { TeamId = team, BasketId = basket, Name = "Credit team", Owner = account.Email, GoalsFor = 4, GoalsAgainst = 3 }] }] });
            await database.SaveChangesAsync();
        }
        var request = new CreateAuctionRequest(group.GroupId, league, year, 0, 0);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/auctions", request)).StatusCode);
        var confirmation = new ConfirmRankCreditsRequest(group.GroupId, basket, year, team, 1, 35, 29);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync("/api/teams/credits/confirm-rank-checkpoint", confirmation)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync("/api/teams/credits/confirm-rank-checkpoint", confirmation with { ExpectedMoneyFromRank = 0 })).StatusCode);
        (await manager.PostAsJsonAsync("/api/teams/credits/confirm-rank-checkpoint", confirmation)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            Assert.Equal(35, roster.MoneyFromRank);
            Assert.Equal(-10, roster.MarketBalanceAdjustment);
            var rank = await database.FantasyStandingSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league && x.SnapshotDay == 0);
            rank.Stages.Single().Teams.Single().GoalsFor = 5;
            await database.SaveChangesAsync();
        }
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        await using var final = factory.Database.CreateContext();
        var saved = await final.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
        Assert.Equal(40, saved.MoneyFromRank); // +5 only once, not a replacement with 34.
        Assert.Equal(34, saved.RankCreditGranted);
        Assert.Equal(-10, saved.MarketBalanceAdjustment);
        Assert.Equal(1, await final.RankCreditConfirmations.CountAsync(x => x.TeamId == team));
    }
}
