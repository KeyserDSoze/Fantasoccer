namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class ExternalLogin
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Subject { get; set; }
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
}
