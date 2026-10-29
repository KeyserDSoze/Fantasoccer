namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Autonomous: own roster, credits, auction, market and lineups.
// Derived: its teams resolve the roster of one origin team (permanent "carbon
// copy"); no own economy, only own competition lineups and results.
public enum BasketKind { Autonomous = 0, Derived = 1 }

// Not the legacy IsMain flag (economy/rules authority among leagues sharing a
// basket). Derived leagues admit teams qualified from source leagues.
public enum LeagueMode { Autonomous = 0, Derived = 1 }

public enum ParticipationSource { BasketEnrollment = 0, Manual = 1, Qualification = 2, Legacy = 3 }

// One origin per derived basket. The constant kind columns take part in the
// composite foreign keys, so the database itself refuses a derived origin
// (no chains, therefore no cycles) or a derivation of an autonomous basket.
public sealed class BasketDerivation
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public Guid OriginBasketId { get; set; }
    public BasketKind DerivedKind { get; set; } = BasketKind.Derived;
    public BasketKind OriginKind { get; set; } = BasketKind.Autonomous;
}

// A derived team has exactly one authoritative origin team of the same group
// and season, located in the origin basket declared by its derived basket.
public sealed class TeamDerivation
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public Guid OriginBasketId { get; set; }
    public Guid OriginTeamId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

// Leagues a derived league takes its participants from (same group/season).
public sealed class LeagueDerivation
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid SourceLeagueId { get; set; }
    public int Ordinal { get; set; }
    // Qualification criterion: the first N of the source league's current standing.
    public int QualifiedTeams { get; set; }
    public string? Stage { get; set; }
}

// Teams actually admitted to a league season. Linking a basket does not imply
// admission: autonomous leagues enrol explicitly, derived leagues qualify.
public sealed class LeagueParticipation
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public Guid BasketId { get; set; }
    public ParticipationSource Source { get; set; }
    public Guid? SourceLeagueId { get; set; }
    public int? QualificationRank { get; set; }
    public DateTimeOffset AdmittedAtUtc { get; set; }
    public Guid? AdmittedByUserId { get; set; }
}

// Maintained by database triggers in the same transaction as ownership,
// derivation and participation changes. Its primary key is the rule: a user
// controls at most one participating team per league and season.
public sealed class LeagueParticipantOwner
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid UserId { get; set; }
    public Guid TeamId { get; set; }
}

// The participant is the "main" lineup of its team: every pre-match save is
// copied to the other competitions where the same authoritative team plays.
// The row's existence is the setting.
public sealed class LeagueLineupAutoCopy
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public DateTimeOffset EnabledAtUtc { get; set; }
    public Guid? EnabledByUserId { get; set; }
}

// Competition-specific lineup: positions only. The roster (players, prices,
// economy) belongs to the authoritative team and is never duplicated here.
public sealed class LeagueLineup
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public int Day { get; set; }
    public int Version { get; set; } = 1;
    public int FormationChanges { get; set; }
    public DateTimeOffset LastUpdateUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public ICollection<LeagueLineupPlayer> Players { get; set; } = [];
    public ICollection<LeagueLineupChange> History { get; set; } = [];
}

public sealed class LeagueLineupPlayer
{
    public string PlayerId { get; set; } = null!;
    public int Position { get; set; }
}

public sealed class LeagueLineupChange
{
    public int Ordinal { get; set; }
    public DateTimeOffset DateUtc { get; set; }
    public required string Author { get; set; }
    public ICollection<LeagueLineupChangePlayer> Players { get; set; } = [];
}

public sealed class LeagueLineupChangePlayer
{
    public int Ordinal { get; set; }
    public required string PlayerName { get; set; }
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
}
