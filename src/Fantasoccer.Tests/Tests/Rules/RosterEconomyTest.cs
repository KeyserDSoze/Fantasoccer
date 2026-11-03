using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class RosterEconomyTest
{
    [Fact]
    public void Every_player_ever_bought_keeps_his_price_and_one_who_left_gives_back_only_his_refund()
    {
        RosterPlayer Row(int status, int price, int revenue = 0) => new() { PlayerId = Guid.NewGuid().ToString("N"), Status = status, Price = price, Revenue = revenue };
        var roster = new RosterSnapshot
        {
            Name = "Economy", MoneyFromRank = 40, MarketBalanceAdjustment = -15, OpeningCompetitionPrize = 30,
            Players = [Row(0, 100), Row(1, 41), Row(2, 30), Row(3, 41, 58), Row(5, 41, 20)],
        };
        // Active 100, old half refund 41-20, discarded 30, sold 41-58, left Serie A 41-20.
        Assert.Equal(100 + 21 + 30 - 17 + 21, RosterEconomy.PlayersCost(roster.Players));
        Assert.Equal(155 - 40 + 15 - 30, RosterEconomy.Cost(roster));
        Assert.Equal(1000 - 100, RosterEconomy.Balance(roster, 1000));
    }

    [Theory]
    [InlineData(41, 50, false, 20)]
    [InlineData(41, 50, true, 21)]
    [InlineData(40, 50, true, 20)]
    [InlineData(41, 0, true, 0)]
    [InlineData(41, 100, false, 41)]
    [InlineData(7, 33, false, 2)]
    [InlineData(7, 33, true, 3)]
    public void The_refund_for_a_player_who_left_Serie_A_follows_the_league_percent_and_rounding(int price, int percent, bool roundUp, int expected)
        => Assert.Equal(expected, RosterEconomy.LeftSerieARefund(price, percent, roundUp));
}
