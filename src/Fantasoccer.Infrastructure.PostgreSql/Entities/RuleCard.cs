namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class CardLibrary
{
    public required string Name { get; set; }
}
public sealed class RuleCard
{
    public required string Library { get; set; }
    public Guid Id { get; set; }
    public int Ordinal { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Type { get; set; }
    public float Points { get; set; }
    public float NegativePoints { get; set; }
    public float EnemyPoints { get; set; }
    public bool IsArchived { get; set; }
    public string? ImageBlobName { get; set; }
    public int Version { get; set; } = 1;
    public RuleCardCondition? MyCondition { get; set; }
    public RuleCardCondition? EnemyCondition { get; set; }
}
public sealed class RuleCardCondition
{
    public ICollection<RuleCardRole> Roles { get; set; } = [];
    public bool HasPlayerCondition { get; set; }
    public int? MinimumNumberOfOccurrences { get; set; }
    public RuleCardPlayerValues? MinimumPlayer { get; set; }
    public RuleCardPlayerValues? MaximumPlayer { get; set; }
    public bool HasTeamCondition { get; set; }
    public RuleCardTeamValues? MinimumTeam { get; set; }
    public RuleCardTeamValues? MaximumTeam { get; set; }
}
public sealed class RuleCardRole { public int Ordinal { get; set; } public int Role { get; set; } }
public sealed class RuleCardPlayerValues
{
    public bool IsPresent { get; set; } = true;
    public float? Vote { get; set; }
    public bool? HasYellow { get; set; }
    public bool? HasRed { get; set; }
    public int? Goals { get; set; }
    public int? Assists { get; set; }
    public int? OwnGoals { get; set; }
    public int? Penalties { get; set; }
    public int? StoppedPenalties { get; set; }
    public int? SufferedGoals { get; set; }
    public int? WrongedPenalties { get; set; }
    public bool? IsManOfTheMatch { get; set; }
    public bool? IsOut { get; set; }
    public bool? IsIn { get; set; }
    public bool? IsInjured { get; set; }
}
public sealed class RuleCardTeamValues
{
    public bool IsPresent { get; set; } = true;
    public int? WithVote { get; set; }
    public int? WithoutVote { get; set; }
    public int? WithSpecial { get; set; }
    public int? Goals { get; set; }
    public int? Penalties { get; set; }
    public int? Assists { get; set; }
    public int? StoppedPenalties { get; set; }
    public int? SufferedGoals { get; set; }
    public int? WrongedPenalties { get; set; }
    public int? OwnGoals { get; set; }
    public int? YellowCards { get; set; }
    public int? RedCards { get; set; }
    public int? EnoughVotes { get; set; }
    public int? ManOfTheMatch { get; set; }
    public int? Injured { get; set; }
}
