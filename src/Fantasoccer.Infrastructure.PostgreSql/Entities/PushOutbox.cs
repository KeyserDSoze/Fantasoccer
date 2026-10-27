namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class PushDayCheckpoint
{
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public bool DeploymentReminder { get; set; }
    public bool EndDay { get; set; }
    public bool LiveChangesReminder { get; set; }
    public bool HasMatchEventVotes { get; set; }
    public long MatchEventSequence { get; set; }
    public ICollection<PushVoteBaseline> Votes { get; set; } = [];
    public ICollection<LegacyFormationPushReceipt> FormationReceipts { get; set; } = [];
}
public sealed class LegacyFormationPushReceipt { public int Ordinal { get; set; } public required string Receipt { get; set; } }
public sealed class LegacyPushActiveDay { public int Ordinal { get; set; } public int SeasonIndex { get; set; } public int Day { get; set; } }
public sealed class PushVoteBaseline
{
    public int Ordinal { get; set; }
    public string PlayerId { get; set; } = null!;
    public string? ClubId { get; set; }
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int VoteRole { get; set; }
    public float Value { get; set; }
    public bool IsFinal { get; set; }
    public int Goals { get; set; }
    public int Penalties { get; set; }
    public int Assists { get; set; }
    public int StoppedPenalties { get; set; }
    public int SufferedGoals { get; set; }
    public int WrongedPenalties { get; set; }
    public int OwnGoals { get; set; }
    public int Status { get; set; }
    public bool ManOfTheMatch { get; set; }
    public bool HasVote { get; set; }
    public bool IsOut { get; set; }
    public bool IsIn { get; set; }
    public bool Injured { get; set; }
}
public enum PushDeliveryStatus { Pending, Completed, Suppressed, Expired, Quarantined, AttemptsExhausted }
public sealed class PushDelivery
{
    public Guid Id { get; set; }
    public required string DeduplicationKey { get; set; }
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? LeagueId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? BasketId { get; set; }
    public int Kind { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public bool HasData { get; set; } = true;
    public string? Uri { get; set; }
    public string? LegacyRecipientEmail { get; set; }
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    public string? Sound { get; set; }
    public string? Tag { get; set; }
    public bool RequireInteraction { get; set; }
    public string? LegacyId { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public PushDeliveryStatus Status { get; set; }
    public string? QuarantineReason { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public ICollection<PushEndpointReceipt> Receipts { get; set; } = [];
}
public sealed class PushEndpointReceipt
{
    public required string EndpointHash { get; set; }
    public string? LegacyEndpoint { get; set; }
    public int Ordinal { get; set; }
    public long? BindingVersion { get; set; }
    public DateTimeOffset? RecordedAtUtc { get; set; }
    public required string Outcome { get; set; }
}
public sealed class PushDeliveryScopeConfirmation
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public required string Uri { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class PushFormationChangeReceipt
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public Guid TeamId { get; set; }
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public int ChangeOrdinal { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
