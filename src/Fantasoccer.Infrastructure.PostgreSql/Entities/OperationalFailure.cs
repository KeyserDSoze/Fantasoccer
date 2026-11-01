namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// A deliberately minimal failure event: never exception messages, URLs, tokens,
// provider responses or imported payloads. Background jobs already have SqlJobRuns.
public sealed class OperationalFailure
{
    public Guid Id { get; set; }
    public required string Source { get; set; }
    public required string Code { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
