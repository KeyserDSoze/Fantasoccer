namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Sharing GroupId and SeasonIndex across both composite foreign keys prevents
// linking a league to a basket from a different group or season, even in raw SQL.
public sealed class LeagueBasketSeason
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public LeagueSeason LeagueSeason { get; set; } = null!;
    public BasketSeason BasketSeason { get; set; } = null!;
}
