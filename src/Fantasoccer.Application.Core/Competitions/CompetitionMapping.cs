using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Competitions;

internal static class CompetitionMapping
{
    public static SavedFantasyResult? Saved(GameResult? row) => row is null ? null : new()
    {
        IsCancelled = row.IsCancelled, HomeGoals = row.HomeGoal, AwayGoals = row.AwayGoal,
        Home = Saved(row.Home), Away = Saved(row.Away)
    };
    private static SavedFantasyPoint? Saved(Point? row) => row is null ? null : new()
    { Value = row.Value, DefensiveBonus = row.DefensiveBonus, GoodPeople = row.GoodPeople, OwnGoal = row.OwnGoal };
    public static GameResult? Domain(SavedFantasyResult? row) => row is null ? null : new()
    {
        IsCancelled = row.IsCancelled, HomeGoal = row.HomeGoals, AwayGoal = row.AwayGoals,
        Home = row.Home is null ? null! : new(row.Home.Value, row.Home.DefensiveBonus, row.Home.GoodPeople, row.Home.OwnGoal),
        Away = row.Away is null ? null! : new(row.Away.Value, row.Away.DefensiveBonus, row.Away.GoodPeople, row.Away.OwnGoal)
    };
    public static Calendar Domain(FantasyCalendar row) => new()
    {
        Year = row.SeasonIndex, Rounds = row.Stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage, stage => stage.Days.OrderBy(x => x.Ordinal)
            .Select(day => new Day { Number = day.FantasyTurn, SerieADay = day.SerieADay, Games = day.Games.OrderBy(x => x.Ordinal).Select(game => new CalendarGame
            { Id = game.Id.ToString(), Number = game.Number, Home = game.HomeName, Away = game.AwayName, HomeOwner = game.HomeOwner!, AwayOwner = game.AwayOwner!, Result = Domain(game.Result) }).ToList() }).ToList())
    };
    public static Player Domain(RosterPlayer row) => new()
    {
        Name = row.Player.Name, RealTeam = row.Club is null ? new() { Name = "", Abbreviation = "" } : new() { Name = row.Club.Name, Abbreviation = row.Club.Abbreviation }, Role = (Role)row.PlayerRole,
        IsActive = row.IsActive, Visible = row.IsVisible, Status = (PlayerInTeamStatus)row.Status, Position = (FantaSoccerRole)row.Position, Price = row.Price, Revenue = row.Revenue
    };
}
