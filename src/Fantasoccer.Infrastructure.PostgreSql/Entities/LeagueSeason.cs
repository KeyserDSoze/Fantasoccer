namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class LeagueSeason
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int CompetitionType { get; set; }
    public LeagueRules? Rules { get; set; }
    public bool IsArchived { get; set; }
    public GroupLeague League { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public ICollection<LeagueBasketSeason> Baskets { get; set; } = [];
}
