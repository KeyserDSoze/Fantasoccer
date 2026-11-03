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
            database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Position = PlayerPosition.Forward });
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

    [PostgreSqlFact]
    public async Task Goal_credits_are_on_for_the_main_league_only_by_default_and_a_team_collects_from_every_league_that_has_them_on()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        var year = FantacalcioTime.ActualYear;
        var group = await CreateGroupAsync(manager);
        var main = await CreateLeagueAsync(manager, group.GroupId, year);
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var account = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var basket = graph.Baskets[0].Id;
        var created = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Two leagues team", account.Id, []));
        created.EnsureSuccessStatusCode();
        var team = (await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        var cupResponse = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/leagues", new CreateLeagueRequest("Cup", year, [basket]));
        cupResponse.EnsureSuccessStatusCode();
        var cup = (await cupResponse.Content.ReadFromJsonAsync<LeagueCreatedDto>())!.LeagueId;
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == main)).IsMain = true;
            if (!await database.LeagueParticipations.AnyAsync(x => x.GroupId == group.GroupId && x.LeagueId == cup && x.TeamId == team && x.SeasonIndex == year))
                database.LeagueParticipations.Add(new() { GroupId = group.GroupId, LeagueId = cup, SeasonIndex = year, TeamId = team, BasketId = basket, Source = ParticipationSource.Manual });
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            roster.RankCreditGranted = 0;
            var club = new FootballClub { Name = "Two leagues club", Abbreviation = "tlc" };
            var player = new FootballPlayer { Name = "Two leagues player " + Guid.NewGuid().ToString("N") };
            database.Clubs.Add(club); database.Players.Add(player);
            database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Position = PlayerPosition.Forward });
            // Default rules: 5 per goal scored, 3 per goal conceded. Main: 4 and 3 = 29. Cup: 2 and 1 = 13.
            foreach (var (league, goals, against, plus) in new[] { (main, 4, 3, 29), (cup, 2, 1, 13) })
                database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 3, Stages =
                    [new() { Stage = "@", Teams = [new() { TeamId = team, BasketId = basket, Name = "Two leagues team", Owner = account.Email, GoalsFor = goals, GoalsAgainst = against, PlusMoney = plus }] }] });
            await database.SaveChangesAsync();
        }
        // Off by default outside the main league: nothing is computed for it, and nothing reaches the team.
        var mainRank = (await manager.GetFromJsonAsync<Fantasoccer.Application.Core.Competitions.FantasyRankDto>($"/api/competitions/{group.GroupId}/{main}/{year}/rank"))!;
        var cupRank = (await manager.GetFromJsonAsync<Fantasoccer.Application.Core.Competitions.FantasyRankDto>($"/api/competitions/{group.GroupId}/{cup}/{year}/rank"))!;
        Assert.Equal(29, mainRank.Rounds["@"][0].PlusMoney);
        Assert.Equal(0, cupRank.Rounds["@"][0].PlusMoney);
        var request = new CreateAuctionRequest(group.GroupId, main, year, 0, 0);
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
            Assert.Equal(29, (await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season)).MoneyFromRank);

        // Switched on for the cup: its credits add up on the team of the basket, once.
        await using (var database = factory.Database.CreateContext())
        {
            (await database.LeagueRules.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == cup && x.SeasonIndex == year)).GoalCreditsEnabled = true;
            await database.SaveChangesAsync();
        }
        cupRank = (await manager.GetFromJsonAsync<Fantasoccer.Application.Core.Competitions.FantasyRankDto>($"/api/competitions/{group.GroupId}/{cup}/{year}/rank"))!;
        Assert.Equal(13, cupRank.Rounds["@"][0].PlusMoney);
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync("/api/auctions", request)).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var saved = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            Assert.Equal(42, saved.MoneyFromRank); Assert.Equal(42, saved.RankCreditGranted);
            // Switched off on the main league explicitly: it stops counting, the cup alone remains (no credits are taken back).
            (await database.LeagueRules.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == main && x.SeasonIndex == year)).GoalCreditsEnabled = false;
            await database.SaveChangesAsync();
        }
        mainRank = (await manager.GetFromJsonAsync<Fantasoccer.Application.Core.Competitions.FantasyRankDto>($"/api/competitions/{group.GroupId}/{main}/{year}/rank"))!;
        Assert.Equal(0, mainRank.Rounds["@"][0].PlusMoney);
        // What the team takes to the next auction is the same from either league: balance - granted + goal credits of the leagues that have them on.
        Assert.Equal(1042 - 42 + 13, mainRank.Rounds["@"][0].ValueAssets);
    }
}
