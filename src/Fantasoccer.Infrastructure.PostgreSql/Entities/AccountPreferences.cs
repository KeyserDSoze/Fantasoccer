namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class AccountPreferences
{
    public Guid UserId { get; set; }
    public string? AiPrompt { get; set; }
    public bool AutoOptimalFormation { get; set; }
    public bool LiveWidgetEnabled { get; set; } = true;
}

public sealed class AccountSelection
{
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? LeagueId { get; set; }
    public int? Year { get; set; }
    public Guid? WorkingUserId { get; set; }
    public UserAccount? WorkingUser { get; set; }
}

// Keep invalid/historical references for reconciliation, rather than silently
// replacing them with today's group or discarding source data.
public sealed class UnresolvedAccountSelection
{
    public Guid UserId { get; set; }
    public string? LegacyGroupId { get; set; }
    public string? LegacyLeagueId { get; set; }
    public int? Year { get; set; }
    public string? WorkingEmail { get; set; }
}
