using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class RankCalculatorRulesTest
{
    private readonly IRankCalculator _calculator;

    public RankCalculatorRulesTest(IRankCalculator calculator)
        => _calculator = calculator;

    [Fact]
    public void AddsWinGoalsPointsAndMoney()
    {
        var settings = LeagueSetting.Default;
        settings.MoneyForGoal = 5;
        settings.MoneyForSufferedGoal = 3;
        var team = Team();

        var calculated = _calculator.AddDay(team, true, Result(3, 1, 70, 65), settings);

        Assert.True(calculated);
        Assert.Equal(3, team.Goal);
        Assert.Equal(1, team.SufferedGoal);
        Assert.Equal(3, team.Point);
        Assert.Equal(1, team.Victories);
        Assert.Equal(18, team.PlusMoney);
        Assert.Equal(70, team.ValuePoint);
    }

    [Fact]
    public void UsesValuePointsWhenConfigured()
    {
        var settings = LeagueSetting.Default;
        settings.RankWithValuePoints = true;
        var team = Team();

        _calculator.AddDay(team, false, Result(1, 2, 60, 75), settings);

        Assert.Equal(78, team.Point);
        Assert.Equal(1, team.Victories);
    }

    [Fact]
    public void IgnoresCancelledAndMissingResults()
    {
        var team = Team();

        Assert.True(_calculator.AddDay(team, true, new GameResult { IsCancelled = true }, LeagueSetting.Default));
        Assert.False(_calculator.AddDay(team, true, null, LeagueSetting.Default));
        Assert.Equal(0, team.Point);
        Assert.Equal(0, team.Goal);
    }

    [Fact]
    public void AddsDrawWithConfiguredPoints()
    {
        var team = Team();

        _calculator.AddDay(team, true, Result(2, 2, 66, 66), LeagueSetting.Default);

        Assert.Equal(1, team.Point);
        Assert.Equal(1, team.Draws);
        Assert.Equal(2, team.Goal);
    }

    private static RankedTeam Team()
        => new() { Name = "Team", Owner = "team@test.local" };

    private static GameResult Result(int homeGoal, int awayGoal, float homeValue, float awayValue)
        => new()
        {
            Home = new Point(homeValue, false, false, false),
            Away = new Point(awayValue, false, false, false),
            HomeGoal = homeGoal,
            AwayGoal = awayGoal
        };
}
