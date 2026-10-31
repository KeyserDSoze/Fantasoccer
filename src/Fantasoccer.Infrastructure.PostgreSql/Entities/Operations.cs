namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Private operational archive, separate from gameplay and telemetry exports.
public sealed class ArchivedOperationalLog
{
    public Guid Id { get; set; }
    public required string PartitionKey { get; set; }
    public required string RowKey { get; set; }
    public DateTimeOffset? TimestampUtc { get; set; }
    // Azure/.NET retain 100 ns ticks; PostgreSQL timestamptz has microsecond
    // precision. Keep exact provenance separately from the indexed instant.
    public long? LegacyTimestampUtcTicks { get; set; }
    public required string ETag { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public ICollection<ArchivedLogProperty> Properties { get; set; } = [];
}
public sealed class ArchivedLogProperty
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string EncodedValue { get; set; }
}
public sealed class SqlJobState
{
    public required string Name { get; set; }
    public int SeasonIndex { get; set; }
    public DateTimeOffset NextRunAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? LastStartedAtUtc { get; set; }
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
    public string? LastStatus { get; set; }
    // Durable keyset progress of a bounded maintenance sweep (currently hall); never a reference to mutable league rows.
    public Guid? CursorGroupId { get; set; }
    public Guid? CursorLeagueId { get; set; }
}
public sealed class SqlJobRun
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int SeasonIndex { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public required string Status { get; set; }
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public string? Code { get; set; }
}
