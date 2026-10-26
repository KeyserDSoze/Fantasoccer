using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Football;

public static class FootballMapping
{
    public static ClubDto Club(FootballClub row) => new(row.Id, row.Name, row.Abbreviation);
    public static VoteDto Vote(FootballVote row) => new(row.VoteRole, row.Value, row.IsFinal, row.Goals, row.Penalties, row.Assists,
        row.StoppedPenalties, row.SufferedGoals, row.WrongedPenalties, row.OwnGoals, row.Status, row.ManOfTheMatch, row.HasVote, row.IsOut, row.IsIn, row.Injured);
    public static ChanceDto Chance(FootballChance row) => new(row.Fantagazzetta, row.Gazzetta, row.Mediaset, row.Sky, row.Status, row.Description,
        row.LegacyLastGameDay is { } day ? new(day, row.LegacyLastGameVote, row.LegacyLastGamePositiveness ?? 0) : null, row.LegacyTrend);
    public static RealTeam DomainClub(FootballClub row) => new() { Name = row.Name, Abbreviation = row.Abbreviation };
    public static global::Fantasoccer.Vote DomainVote(FootballVote row) => new()
    {
        Role = (Role)row.VoteRole, Value = row.Value, IsFinal = row.IsFinal, Goal = row.Goals, Penalty = row.Penalties, Assist = row.Assists,
        StoppedPenalty = row.StoppedPenalties, SufferedGoal = row.SufferedGoals, WrongedPenalty = row.WrongedPenalties, OwnGoal = row.OwnGoals,
        Status = (Behaviour)row.Status, ManOfTheMatch = row.ManOfTheMatch, HasVote = row.HasVote, IsOut = row.IsOut, IsIn = row.IsIn, Injured = row.Injured
    };
}
