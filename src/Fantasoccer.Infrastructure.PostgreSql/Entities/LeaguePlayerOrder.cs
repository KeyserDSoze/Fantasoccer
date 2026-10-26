namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class LeaguePlayerOrder
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<LeaguePlayerOrderEntry> Players { get; set; } = [];
}
public sealed class LeaguePlayerOrderEntry
{
    public int Ordinal { get; set; }
    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}
