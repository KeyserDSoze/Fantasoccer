namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class UserPushPreferences
{
    public Guid UserId { get; set; }
    public bool MatchEvents { get; set; } = true;
    public bool OpponentMatchEvents { get; set; }
    public bool DeploymentReminder { get; set; }
    public bool EndDay { get; set; }
    public bool MarketEvents { get; set; }
    public bool LiveChangesReminder { get; set; }
    public bool LiveFormationChangeEvents { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<LegacyPushSubscription> LegacySubscriptions { get; set; } = [];
}
public sealed class LegacyPushSubscription
{
    public int Ordinal { get; set; }
    public string? Endpoint { get; set; }
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
    public bool IsValid { get; set; }
}
public sealed class PushDeviceBinding
{
    public required string EndpointHash { get; set; }
    public Guid UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public bool IsValid { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
    // Only an explicit registration response returns the random device capability.
    // GET settings, MCP reads and notification payloads never disclose it.
    public string? DeviceProofHash { get; set; }
}
public sealed class PushDeviceOwnershipEvent
{
    public Guid Id { get; set; }
    public required string EndpointHash { get; set; }
    public Guid? PreviousUserId { get; set; }
    public Guid UserId { get; set; }
    public long BindingVersion { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
