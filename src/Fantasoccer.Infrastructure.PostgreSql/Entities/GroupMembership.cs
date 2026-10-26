namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// These are group-scoped roles, not permission to administer the application.
// Values match the legacy flags; importing conflicting copies still requires reconciliation.
[Flags]
public enum GroupRole
{
    None = 0,
    Reader = 1,
    Participant = 2,
    Admin = 4,
    SuperAdmin = 8
}

public sealed class GroupMembership
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public GroupRole Roles { get; set; }
    public FantasyGroup Group { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}
