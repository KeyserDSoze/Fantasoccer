using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Teams;

// One definition of a team's money, shared by auctions, market, standings and the roster screens.
// Every roster row ever bought keeps its price; a player who left the roster gives back only his refund.
public static class RosterEconomy
{
    public const int Active = 0, SoldForOneHalf = 1, Discarded = 2, Sold = 3, LeftSerieA = 5;

    // Credits given back by a row: half price for the old "injured/abroad" status, the sale price for a sale, the refund
    // fixed by the league rules (at that moment) for a player who left Serie A. Active and discarded rows give nothing back.
    public static long Refund(RosterPlayer player) => player.Status switch
    {
        SoldForOneHalf => player.Price / 2,
        Sold or LeftSerieA => player.Revenue,
        _ => 0,
    };

    public static long PlayersCost(IEnumerable<RosterPlayer> players) => players.Sum(x => (long)x.Price - Refund(x));

    // Net cost: what the players cost, minus credits received from standings, market and the opening competition.
    public static long Cost(RosterSnapshot roster) =>
        PlayersCost(roster.Players) - roster.MoneyFromRank - roster.MarketBalanceAdjustment - roster.OpeningCompetitionPrize;

    public static long Balance(RosterSnapshot roster, int startingMoney) => startingMoney - Cost(roster);

    // Share of the price given back for a player who left Serie A (league rule; default half, rounded down).
    public static int LeftSerieARefund(int price, int percent, bool roundUp)
    {
        var share = (long)Math.Max(0, price) * Math.Clamp(percent, 0, 100);
        return (int)(roundUp ? (share + 99) / 100 : share / 100);
    }
}
