namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballMatchday
{
    public int SeasonIndex { get; set; }
    public int SerieADay { get; set; }
    public ICollection<FootballMatch> Matches { get; set; } = [];
}
public sealed class FootballMatch
{
    public int SeasonIndex { get; set; }
    public int SerieADay { get; set; }
    public int Ordinal { get; set; }
    public string HomeClubId { get; set; } = null!;
    public string AwayClubId { get; set; } = null!;
    public DateTimeOffset? KickoffUtc { get; set; }
    public DateTime? LegacyKickoffLocal { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public bool Delayed { get; set; }
    public FootballClub HomeClub { get; set; } = null!;
    public FootballClub AwayClub { get; set; } = null!;
}
