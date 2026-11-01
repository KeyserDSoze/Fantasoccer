namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Provider evidence kept apart from players, rosters and scoring. A coach can
// change club during a season, so the club is part of this day's identity.
public sealed class FootballCoachVote
{
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public string ClubId { get; set; } = null!;
    public string CoachKey { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Role { get; set; } = (int)Fantasoccer.Role.Coach;
    public float Value { get; set; }
    public bool HasVote { get; set; }
    public bool IsFinal { get; set; }
    public string ProviderVoteJson { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}
