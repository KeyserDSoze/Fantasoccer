namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Protocol metadata is intentionally JSONB: OpenIddict extensions vary by client.
// Game entities, memberships and rosters never use this document representation.
public sealed class OAuthRecord
{
    public required string Id { get; set; }
    public required string Kind { get; set; }
    public required string Version { get; set; }
    public string? Payload { get; set; }
    public DateTimeOffset? DeleteAfterUtc { get; set; }
}
