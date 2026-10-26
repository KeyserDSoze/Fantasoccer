namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class AuctionSession
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid Id { get; set; }
    public Guid CreatorUserId { get; set; }
    public required string CreatorEmail { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTime? LegacyCreatedUtc { get; set; }
    public int Type { get; set; }
    public int Kind { get; set; }
    public int Status { get; set; }
    public int SecondsPerAuction { get; set; } = 10;
    public int CurrentRole { get; set; }
    public int Version { get; set; } = 1;
    public AuctionCurrentLot? Actual { get; set; }
    public ICollection<AuctionPlayerEntry> PlayerOrder { get; set; } = [];
    public ICollection<AuctionLastShown> LastShown { get; set; } = [];
    public ICollection<AuctionWinning> Winnings { get; set; } = [];
    public ICollection<AuctionLegacyParticipant> LegacyParticipants { get; set; } = [];
}
public sealed class AuctionPlayerEntry
{
    public int Role { get; set; }
    public int Ordinal { get; set; }
    public Guid PlayerId { get; set; }
    public bool IsShown { get; set; }
}
public sealed class AuctionLastShown
{
    public int Role { get; set; }
    public Guid? PlayerId { get; set; }
}
public sealed class AuctionWinning
{
    public int Ordinal { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? BasketId { get; set; }
    public string? OwnerEmail { get; set; }
    public int Price { get; set; }
}
// Persisted legacy participants were not reliable connection presence. Preserve
// the source as audit only; never let them grant bidding rights or live presence.
public sealed class AuctionLegacyParticipant
{
    public int Ordinal { get; set; }
    public required string Name { get; set; }
    public required string OwnerEmail { get; set; }
    public ICollection<AuctionLegacyCoOwner> CoOwners { get; set; } = [];
}
public sealed class AuctionLegacyCoOwner { public int Ordinal { get; set; } public required string Email { get; set; } }

public sealed class AuctionCurrentLot
{
    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? TimerStartedAtUtc { get; set; }
    public DateTime? LegacyStartedUtc { get; set; }
    public DateTime? LegacyTimerStartedUtc { get; set; }
    public Guid? WinningTeamId { get; set; }
    public Guid? WinningBasketId { get; set; }
    public string? OwnerEmail { get; set; }
    public Guid? SubstitutedPlayerId { get; set; }
    public int Price { get; set; }
    public AuctionPlayerStatistic Statistics { get; set; } = new();
}
// An immutable displayed-player snapshot, not a second global player catalogue.
// Import must retain statistics actually shown in a historical active lot.
public sealed class AuctionPlayerStatistic
{
    public float Summatory { get; set; }
    public float FantaSummatory { get; set; }
    public int WithVote { get; set; }
    public int WithoutVote { get; set; }
    public int NoPlayed { get; set; }
    public int WithSpecial { get; set; }
    public int Goals { get; set; }
    public int Penalties { get; set; }
    public int Assists { get; set; }
    public int StoppedPenalties { get; set; }
    public int SufferedGoals { get; set; }
    public int WrongedPenalties { get; set; }
    public int OwnGoals { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int EnoughVotes { get; set; }
    public int ManOfTheMatch { get; set; }
    public int Injured { get; set; }
    public bool HasDescription { get; set; }
    public string? DescriptionTop { get; set; }
    public string? DescriptionDown { get; set; }
    public bool HasGameHistory { get; set; }
    public ICollection<FootballStatisticGame> Games { get; set; } = [];
}

public sealed class AuctionPresence
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid AuctionId { get; set; }
    public required string ConnectionId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ConnectedAtUtc { get; set; }
}

public sealed class AuctionRosterChange
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid AuctionId { get; set; }
    public Guid TeamId { get; set; }
    public Guid BasketId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid ActorUserId { get; set; }
    public int RosterOrdinal { get; set; }
    public int PreviousStatus { get; set; }
    public int Status { get; set; }
    public int Price { get; set; }
    public int Revenue { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
}
