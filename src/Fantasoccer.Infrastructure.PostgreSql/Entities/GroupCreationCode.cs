namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// Bearer capability for one group. Only its SHA-256 digest is persisted; the
// random code is shown to the issuing platform administrator exactly once.
public sealed class GroupCreationCode
{
    public Guid Id { get; set; }
    public required string CodeHash { get; set; }
    public required string Label { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public Guid? RedeemedGroupId { get; set; }
    public DateTimeOffset? RedeemedAtUtc { get; set; }
}
