namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public enum RosterKind { Season = 0, Matchday = 1, OpeningSeason = 2, OpeningMatchday = 3 }

public sealed class RosterSnapshot
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public int TeamOriginYear { get; set; }
    public RosterKind Kind { get; set; }
    public int Day { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid? LeagueId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public int MoneyFromRank { get; set; }
    public int MarketBalanceAdjustment { get; set; }
    // Legacy balances did not record how many rank credits were already awarded.
    // null requires explicit confirmation before a new automatic award.
    public int? RankCreditGranted { get; set; }
    public int OpeningCompetitionPrize { get; set; }
    public int FormationChanges { get; set; }
    public DateTime? LegacyLastUpdateLocal { get; set; }
    public DateTimeOffset? LastUpdateUtc { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<RosterPlayer> Players { get; set; } = [];
    public ICollection<RosterOwner> AdditionalOwners { get; set; } = [];
    public ICollection<RosterFormationChange> FormationHistory { get; set; } = [];
}

public sealed class RosterPlayer
{
    public int Ordinal { get; set; }
    public string PlayerId { get; set; } = null!;
    // NULL only for legacy snapshots that never recorded the day's club.
    public string? ClubId { get; set; }
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int Price { get; set; }
    public int Revenue { get; set; }
    public int Status { get; set; }
    public int Position { get; set; }
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub? Club { get; set; }
}

public sealed class RosterOwner { public Guid UserId { get; set; } public UserAccount User { get; set; } = null!; }
public sealed class RosterFormationChange
{
    public int Ordinal { get; set; }
    public DateTime? LegacyDateLocal { get; set; }
    public DateTimeOffset? DateUtc { get; set; }
    public required string Author { get; set; }
    public ICollection<RosterPlayerChange> Players { get; set; } = [];
}
public sealed class RosterPlayerChange
{
    public int Ordinal { get; set; }
    public required string PlayerName { get; set; }
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
}
