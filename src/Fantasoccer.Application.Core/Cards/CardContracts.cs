namespace Fantasoccer.Application.Core.Cards;

public sealed record CardPlayerValuesDto(float? Vote = null, bool? HasYellow = null, bool? HasRed = null, int? Goals = null, int? Assists = null,
    int? OwnGoals = null, int? Penalties = null, int? StoppedPenalties = null, int? SufferedGoals = null, int? WrongedPenalties = null,
    bool? IsManOfTheMatch = null, bool? IsOut = null, bool? IsIn = null, bool? IsInjured = null);
public sealed record CardTeamValuesDto(int? WithVote = null, int? WithoutVote = null, int? WithSpecial = null, int? Goals = null, int? Penalties = null,
    int? Assists = null, int? StoppedPenalties = null, int? SufferedGoals = null, int? WrongedPenalties = null, int? OwnGoals = null, int? YellowCards = null,
    int? RedCards = null, int? EnoughVotes = null, int? ManOfTheMatch = null, int? Injured = null);
public sealed record CardPlayerConditionDto(int? MinimumNumberOfOccurrences, CardPlayerValuesDto? Minimum, CardPlayerValuesDto? Maximum);
public sealed record CardTeamConditionDto(CardTeamValuesDto? Maximum, CardTeamValuesDto? Minimum);
public sealed record CardConditionDto(int[] RolesInvolved, CardPlayerConditionDto? ConditionForPlayer, CardTeamConditionDto? Condition);
public sealed record CardDto(Guid Id, string Name, string Description, int Type, float Points, float NegativePoints, float EnemyPoints,
    CardConditionDto? MyCondition, CardConditionDto? EnemyCondition, string? ImageUrl, int Version);
public sealed record WriteCardRequest(string Name, string Description, int Type, float Points, float NegativePoints, float EnemyPoints,
    CardConditionDto? MyCondition = null, CardConditionDto? EnemyCondition = null, int? ExpectedVersion = null);
public interface ICardImageStorage
{
    Task UploadAsync(string blobName, byte[] image, string contentType, CancellationToken cancellationToken);
}
