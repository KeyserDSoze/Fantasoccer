namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Import and interactive writes share a transaction lock. Once writes start,
// importing a legacy snapshot over the new data is permanently refused.
public sealed class CoreMigrationState
{
    public int Id { get; set; } = 1;
    public string? SourceId { get; set; }
    public DateTimeOffset? InteractiveWritesStartedAtUtc { get; set; }
}

public sealed class LegacyIdentityMap
{
    public required string SourceId { get; set; }
    public required string Kind { get; set; }
    public required string LegacyKey { get; set; }
    public Guid TargetId { get; set; }
}

public sealed class CoreImportCheckpoint
{
    public required string SourceId { get; set; }
    public required string Container { get; set; }
    public required string BlobName { get; set; }
    public required string Sha256 { get; set; }
    public int MappingVersion { get; set; }
}
