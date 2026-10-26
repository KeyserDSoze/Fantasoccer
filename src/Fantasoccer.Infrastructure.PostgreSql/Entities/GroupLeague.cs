namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class GroupLeague
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public required string Name { get; set; }
    public bool IsMain { get; set; }
    public int CompetitionType { get; set; }
    public bool IsArchived { get; set; }
    public FantasyGroup Group { get; set; } = null!;
    public ICollection<LeagueSeason> Seasons { get; set; } = [];
    public ICollection<GroupLeagueBasket> BasketLinks { get; set; } = [];
}
