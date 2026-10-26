using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Cards;

public static class CardRulesMapping
{
    public static CardConditionDto? ToDto(RuleCardCondition? row) => row is null ? null : new(row.Roles.OrderBy(x => x.Ordinal).Select(x => x.Role).ToArray(),
        !row.HasPlayerCondition ? null : new(row.MinimumNumberOfOccurrences, ToDto(row.MinimumPlayer), ToDto(row.MaximumPlayer)),
        !row.HasTeamCondition ? null : new(ToDto(row.MaximumTeam), ToDto(row.MinimumTeam)));
    private static CardPlayerValuesDto? ToDto(RuleCardPlayerValues? x) => x is null ? null : new(x.Vote, x.HasYellow, x.HasRed, x.Goals, x.Assists, x.OwnGoals,
        x.Penalties, x.StoppedPenalties, x.SufferedGoals, x.WrongedPenalties, x.IsManOfTheMatch, x.IsOut, x.IsIn, x.IsInjured);
    private static CardTeamValuesDto? ToDto(RuleCardTeamValues? x) => x is null ? null : new(x.WithVote, x.WithoutVote, x.WithSpecial, x.Goals, x.Penalties, x.Assists,
        x.StoppedPenalties, x.SufferedGoals, x.WrongedPenalties, x.OwnGoals, x.YellowCards, x.RedCards, x.EnoughVotes, x.ManOfTheMatch, x.Injured);
    public static RuleCardCondition? FromDto(CardConditionDto? row)
    {
        if (row is null) return null;
        if (row.RolesInvolved is null || row.RolesInvolved.Length > 32 || row.RolesInvolved.Any(role => role is < -2 or > 11) || row.ConditionForPlayer?.MinimumNumberOfOccurrences < 0)
            throw new ApplicationFault(400, "invalid_card_condition", "Ruoli o numero minimo di occorrenze non validi.");
        return new() { Roles = row.RolesInvolved.Select((role, ordinal) => new RuleCardRole { Role = role, Ordinal = ordinal }).ToArray(),
            HasPlayerCondition = row.ConditionForPlayer != null, MinimumNumberOfOccurrences = row.ConditionForPlayer?.MinimumNumberOfOccurrences,
            MinimumPlayer = FromDto(row.ConditionForPlayer?.Minimum), MaximumPlayer = FromDto(row.ConditionForPlayer?.Maximum),
            HasTeamCondition = row.Condition != null, MinimumTeam = FromDto(row.Condition?.Minimum), MaximumTeam = FromDto(row.Condition?.Maximum) };
    }
    private static RuleCardPlayerValues? FromDto(CardPlayerValuesDto? x)
    {
        if (x is null) return null;
        if (x.Vote is { } vote && !float.IsFinite(vote)) throw new ApplicationFault(400, "invalid_card_vote", "La soglia voto non è valida.");
        return new() { Vote = x.Vote, HasYellow = x.HasYellow, HasRed = x.HasRed, Goals = x.Goals, Assists = x.Assists, OwnGoals = x.OwnGoals,
            Penalties = x.Penalties, StoppedPenalties = x.StoppedPenalties, SufferedGoals = x.SufferedGoals, WrongedPenalties = x.WrongedPenalties,
            IsManOfTheMatch = x.IsManOfTheMatch, IsOut = x.IsOut, IsIn = x.IsIn, IsInjured = x.IsInjured };
    }
    private static RuleCardTeamValues? FromDto(CardTeamValuesDto? x) => x is null ? null : new() { WithVote = x.WithVote, WithoutVote = x.WithoutVote,
        WithSpecial = x.WithSpecial, Goals = x.Goals, Penalties = x.Penalties, Assists = x.Assists, StoppedPenalties = x.StoppedPenalties, SufferedGoals = x.SufferedGoals,
        WrongedPenalties = x.WrongedPenalties, OwnGoals = x.OwnGoals, YellowCards = x.YellowCards, RedCards = x.RedCards, EnoughVotes = x.EnoughVotes, ManOfTheMatch = x.ManOfTheMatch, Injured = x.Injured };

    public static Card ToDomain(RuleCard row) => new() { Id = row.Id.ToString(), Name = row.Name, Description = row.Description, Type = (CardType)row.Type,
        Points = row.Points, NegativePoints = row.NegativePoints, EnemyPoints = row.EnemyPoints, MyCondition = Domain(row.MyCondition), EnemyCondition = Domain(row.EnemyCondition) };
    private static CardCondition? Domain(RuleCardCondition? row) => row is null ? null : new()
    {
        RolesInvolved = row.Roles.OrderBy(x => x.Ordinal).Select(x => (FantaSoccerRole)x.Role).ToList(),
        ConditionForPlayer = !row.HasPlayerCondition ? null : new() { MinimumNumberOfOccurencies = row.MinimumNumberOfOccurrences, Minimum = Domain(row.MinimumPlayer), Maximum = Domain(row.MaximumPlayer) },
        Condition = !row.HasTeamCondition ? null : new() { Minimum = Domain(row.MinimumTeam), Maximum = Domain(row.MaximumTeam) }
    };
    private static CardConditionForPlayersValues? Domain(RuleCardPlayerValues? x) => x is null ? null : new()
    { Vote = x.Vote, HasYellow = x.HasYellow, HasRed = x.HasRed, Goals = x.Goals, Assists = x.Assists, OwnGoals = x.OwnGoals, Penalties = x.Penalties,
        StoppedPenalties = x.StoppedPenalties, SufferedGoals = x.SufferedGoals, WrongedPenalties = x.WrongedPenalties, IsManOfTheMatch = x.IsManOfTheMatch, IsOut = x.IsOut, IsIn = x.IsIn, IsInjured = x.IsInjured };
    private static CardConditionSummatoryPlayers? Domain(RuleCardTeamValues? x) => x is null ? null : new()
    { WithVote = x.WithVote, WithoutVote = x.WithoutVote, WithSpecial = x.WithSpecial, Goal = x.Goals, Penalty = x.Penalties, Assist = x.Assists,
        StoppedPenalty = x.StoppedPenalties, SufferedGoal = x.SufferedGoals, WrongedPenalty = x.WrongedPenalties, OwnGoal = x.OwnGoals, YellowCard = x.YellowCards,
        RedCard = x.RedCards, IsEnough = x.EnoughVotes, ManOfTheMatch = x.ManOfTheMatch, Injured = x.Injured };
}
