using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Market;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Market_votes_and_roster_transfers_are_atomic_server_priced_and_preserve_sold_history_and_concurrency()
    {
        using var buyer = await factory.AccountAsync(globalAdmin: true);
        using var seller = await factory.AccountAsync();
        using var voter = await factory.AccountAsync();
        using var coVoter = await factory.AccountAsync();
        using var fourth = await factory.AccountAsync();
        using var outsider = await factory.AccountAsync();
        var year = FantacalcioTime.ActualYear;
        var group = await CreateGroupAsync(buyer);
        var league = await CreateLeagueAsync(buyer, group.GroupId, year);
        var graph = (await buyer.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        var accounts = new List<AccountIdentityDto>();
        foreach (var client in new[] { buyer, seller, voter, fourth, coVoter })
            accounts.Add((await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!);
        (await seller.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(MarketEvents: true))).EnsureSuccessStatusCode();
        foreach (var account in accounts.Skip(1))
            (await buyer.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(account.Email, "Member", 2))).EnsureSuccessStatusCode();
        var teamIds = new List<Guid>();
        foreach (var (account, index) in accounts.Take(4).Select((account, index) => (account, index)))
        {
            var result = await buyer.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Market team " + index,
                account.Id, index == 2 ? [accounts[4].Id] : []));
            result.EnsureSuccessStatusCode();
            teamIds.Add((await result.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId);
        }
        var playerIds = new List<Guid>();
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).IsMain = true;
            var club = new FootballClub { Name = "Runtime market club", Abbreviation = "mkt" };
            var otherClub = new FootballClub { Name = "Runtime market other club", Abbreviation = "mko" };
            database.Clubs.AddRange(club, otherClub);
            foreach (var (day, kickoff) in new[] { (35, DateTimeOffset.UtcNow.AddDays(-2)), (36, DateTimeOffset.UtcNow.AddDays(2)) })
                database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
                    [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClub = club, AwayClub = otherClub, KickoffUtc = kickoff }] });
            for (var i = 0; i < 2; i++)
            {
                var player = new FootballPlayer { Name = "Market player " + Guid.NewGuid().ToString("N") };
                database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, Position = PlayerPosition.Forward });
                var roster = await database.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == teamIds[i] && x.Kind == RosterKind.Season);
                roster.Players.Add(new() { Ordinal = 0, Player = player, Club = club, PlayerRole = 3, Price = i == 0 ? 50 : 80, Position = 3, Status = 0 });
                if (i == 0) roster.Players.Add(new() { Ordinal = 1, Player = player, Club = club, PlayerRole = 3, Price = 10, Revenue = 15, Position = 11, Status = 3 });
                foreach (var day in new[] { 35, 36 })
                    database.RosterSnapshots.Add(new() { GroupId = group.GroupId, BasketId = basket, SeasonIndex = year, TeamOriginYear = year, TeamId = teamIds[i],
                        Kind = RosterKind.Matchday, Day = day, CompetitionId = Guid.Empty, Name = roster.Name, OwnerUserId = roster.OwnerUserId,
                        Players = [new() { Ordinal = 0, Player = player, Club = club, PlayerRole = 3, Price = i == 0 ? 50 : 80, Position = day == 36 && i == 0 ? 11 : 3, Status = 0 }] });
                await database.SaveChangesAsync();
                playerIds.Add(player.Id);
            }
        }
        var path = $"/api/competitions/{group.GroupId}/{league}/{year}/market";
        var request = new CreateMarketRequest(teamIds[0], teamIds[1], [playerIds[0]], [playerIds[1]], 10, 0, 1, 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync(path, request with { BuyerPlayerIds = [Guid.NewGuid()] })).StatusCode);
        var response = await buyer.PostAsJsonAsync(path, new { request.BuyerTeamId, request.SellerTeamId, request.BuyerPlayerIds, request.SellerPlayerIds,
            request.MoneyFromBuyer, request.MoneyFromSeller, request.ExpectedBuyerRosterVersion, request.ExpectedSellerRosterVersion, buyerPrices = new[] { 0 }, status = 1, approvers = accounts.Select(x => x.Email) });
        response.EnsureSuccessStatusCode();
        var first = (await response.Content.ReadFromJsonAsync<MarketOperationDto>())!.Market;
        Assert.Equal(50, first.BuyerPlayers[0].Price);
        Assert.Single(first.Approvers);
        Assert.False(first.SellerAccepted);
        var duplicateResponse = await buyer.PostAsJsonAsync(path, request);
        duplicateResponse.EnsureSuccessStatusCode();
        var duplicate = (await duplicateResponse.Content.ReadFromJsonAsync<MarketOperationDto>())!.Market;
        Assert.Equal(HttpStatusCode.BadRequest, (await voter.PostAsJsonAsync(path + $"/{first.Id}/approve", new MarketCommandRequest(first.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync(path + $"/{first.Id}/accept", new MarketCommandRequest(first.Version))).StatusCode);
        var accepted = await seller.PostAsJsonAsync(path + $"/{first.Id}/accept", new MarketCommandRequest(first.Version));
        accepted.EnsureSuccessStatusCode();
        var pending = (await accepted.Content.ReadFromJsonAsync<MarketOperationDto>())!.Market;
        Assert.Equal(0, pending.Status);
        var triggerName = "market_failure_" + Guid.NewGuid().ToString("N");
        await using (var database = factory.Database.CreateContext())
        {
            await database.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION {triggerName}() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."TeamId" = '{teamIds[1]}'::uuid AND NEW."PlayerId" = '{playerIds[0]}'::uuid THEN
                        RAISE EXCEPTION 'Synthetic market transaction failure';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER {triggerName} AFTER INSERT ON "RosterPlayers" FOR EACH ROW EXECUTE FUNCTION {triggerName}();
                """);
        }
        try
        {
            // TestServer may surface the unhandled DbUpdateException or return a
            // 500 response. Either way, the entire multi-roster write must roll back.
            HttpResponseMessage? failedResponse = null;
            Exception? failure = null;
            try { failedResponse = await voter.PostAsJsonAsync(path + $"/{first.Id}/approve", new MarketCommandRequest(pending.Version)); }
            catch (Exception error) { failure = error; }
            Assert.True(failure != null || failedResponse?.StatusCode == HttpStatusCode.InternalServerError);
            await using var database = factory.Database.CreateContext();
            var unchanged = await database.MarketTrades.SingleAsync(x => x.Id == first.Id && x.GroupId == group.GroupId);
            Assert.Equal(pending.Version, unchanged.Version);
            Assert.Equal(0, unchanged.Status);
            Assert.Equal(2, unchanged.Votes.Count);
            Assert.Equal(2, await database.MarketTradeEvents.CountAsync(x => x.TradeId == first.Id && x.GroupId == group.GroupId));
            var original = await database.RosterSnapshots.SingleAsync(x => x.TeamId == teamIds[0] && x.GroupId == group.GroupId && x.Kind == RosterKind.Season);
            Assert.Equal(playerIds[0], original.Players.Single(x => x.Status == 0).PlayerId);
            Assert.Equal(0, original.MoneyFromRank);
        }
        finally
        {
            await using var database = factory.Database.CreateContext();
            await database.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS {triggerName} ON \"RosterPlayers\"; DROP FUNCTION IF EXISTS {triggerName}();");
        }
        var results = await Task.WhenAll(voter.PostAsJsonAsync(path + $"/{first.Id}/approve", new MarketCommandRequest(pending.Version)),
            coVoter.PostAsJsonAsync(path + $"/{first.Id}/approve", new MarketCommandRequest(pending.Version)));
        Assert.Single(results.Where(x => x.IsSuccessStatusCode));
        Assert.Single(results.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        var saved = (await buyer.GetFromJsonAsync<MarketDto[]>(path))!.Single(x => x.Id == first.Id);
        Assert.Equal(1, saved.Status);
        Assert.Equal(3, saved.Approvers.Length);
        (await seller.PostAsJsonAsync(path + $"/{duplicate.Id}/accept", new MarketCommandRequest(duplicate.Version))).EnsureSuccessStatusCode();
        var blocked = await voter.PostAsJsonAsync(path + $"/{duplicate.Id}/approve", new MarketCommandRequest(duplicate.Version + 1));
        blocked.EnsureSuccessStatusCode();
        var failed = (await blocked.Content.ReadFromJsonAsync<MarketOperationDto>())!;
        Assert.False(failed.IsOk);
        Assert.Equal(5, failed.Market.Status);
        await using var final = factory.Database.CreateContext();
        var buyerRoster = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == teamIds[0] && x.Kind == RosterKind.Season);
        var sellerRoster = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == teamIds[1] && x.Kind == RosterKind.Season);
        Assert.Equal(playerIds[1], buyerRoster.Players.Single(x => x.Status == 0).PlayerId);
        Assert.Equal(80, buyerRoster.Players.Single(x => x.Status == 0).Price);
        Assert.Equal(playerIds[0], sellerRoster.Players.Single(x => x.Status == 0).PlayerId);
        Assert.Equal(50, sellerRoster.Players.Single(x => x.Status == 0).Price);
        Assert.Equal(15, buyerRoster.Players.Single(x => x.Status == 3).Revenue);
        Assert.Equal(0, buyerRoster.MoneyFromRank);
        Assert.Equal(0, sellerRoster.MoneyFromRank);
        Assert.Equal(-10, buyerRoster.MarketBalanceAdjustment);
        Assert.Equal(10, sellerRoster.MarketBalanceAdjustment);
        Assert.Equal(2, buyerRoster.Version);
        Assert.Equal(2, sellerRoster.Version);
        var historical = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == teamIds[0] && x.Kind == RosterKind.Matchday && x.Day == 35);
        Assert.Equal(playerIds[0], Assert.Single(historical.Players).PlayerId);
        var upcoming = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == teamIds[0] && x.Kind == RosterKind.Matchday && x.Day == 36);
        Assert.Equal(playerIds[1], upcoming.Players.Single(x => x.Status == 0).PlayerId);
        Assert.Equal(11, upcoming.Players.Single(x => x.Status == 0).Position);
        Assert.Equal(3, await final.MarketTradeEvents.CountAsync(x => x.GroupId == group.GroupId && x.TradeId == first.Id));
        Assert.All(await final.MarketTradeEvents.Where(x => x.GroupId == group.GroupId).ToArrayAsync(), x => Assert.Null(x.DeliveredAtUtc));
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<MarketPushProducer>().RunAsync();
        var queued = await final.PushDeliveries.CountAsync(x => x.UserId == accounts[1].Id && x.Kind == 4);
        Assert.True(queued > 0);
        Assert.All(await final.PushDeliveries.Where(x => x.UserId == accounts[1].Id && x.Kind == 4).ToArrayAsync(), x =>
        { Assert.Equal(group.GroupId, x.GroupId); Assert.Equal(league, x.LeagueId); Assert.Equal(year, x.SeasonIndex); });
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<MarketPushProducer>().RunAsync();
        Assert.Equal(queued, await final.PushDeliveries.CountAsync(x => x.UserId == accounts[1].Id && x.Kind == 4));
    }
}
