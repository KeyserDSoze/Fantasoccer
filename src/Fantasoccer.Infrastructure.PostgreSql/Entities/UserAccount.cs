namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsHistoricalPlaceholder { get; set; }
    public ICollection<GroupMembership> Memberships { get; set; } = [];
}
