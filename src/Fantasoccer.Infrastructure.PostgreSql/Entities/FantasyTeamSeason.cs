namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FantasyTeamSeason
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public bool IsArchived { get; set; }
    public string? LogoBlobName { get; set; }
    public BasketSeason BasketSeason { get; set; } = null!;
    public ICollection<TeamOwnership> Owners { get; set; } = [];
}

public sealed class TeamOwnershipChange
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public Guid ActorUserId { get; set; }
    public bool IsPrimary { get; set; }
    public bool Added { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
}

public sealed class TeamOwnership
{
    public Guid GroupId { get; set; }
    public Guid TeamId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public FantasyTeamSeason Team { get; set; } = null!;
    public GroupMembership Membership { get; set; } = null!;
}

public sealed class RankCreditConfirmation
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public Guid ActorUserId { get; set; }
    public int? PreviousGranted { get; set; }
    public int Granted { get; set; }
    public int BalanceAtConfirmation { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
