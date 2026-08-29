namespace Fantasoccer.Tests.Tests.Rules;

public sealed class GameResultRulesTest
{
    [Theory]
    [InlineData(65.5f, 65.5f, 0, 0)]
    [InlineData(66, 65.5f, 1, 0)]
    [InlineData(71.5f, 65.5f, 1, 0)]
    [InlineData(72, 65.5f, 2, 0)]
    [InlineData(78, 66, 3, 1)]
    public void CalculatesGoalsFromConfiguredThresholds(
        float homeValue,
        float awayValue,
        int homeGoals,
        int awayGoals)
    {
        var result = Result(homeValue, awayValue);

        var goals = result.CalculateGoals(LeagueSetting.Default);

        Assert.Equal((homeGoals, awayGoals), goals);
    }

    [Fact]
    public void UsesCustomGoalThresholds()
    {
        var settings = LeagueSetting.Default;
        settings.PointForFirstGoal = 60;
        settings.PointForNextGoal = 5;
        var result = Result(70, 64);

        result.CalculateGoals(settings);

        Assert.Equal(3, result.HomeGoal);
        Assert.Equal(1, result.AwayGoal);
    }

    [Fact]
    public void AwardsLowScoreGoalForConfiguredDifference()
    {
        var settings = LeagueSetting.Default;
        settings.DifferencePointForOwnGoal = 6;
        var result = Result(65, 59);

        result.CalculateGoals(settings);

        Assert.Equal(1, result.HomeGoal);
        Assert.Equal(0, result.AwayGoal);
    }

    private static GameResult Result(float homeValue, float awayValue)
        => new()
        {
            Home = new Point(homeValue, false, false, false),
            Away = new Point(awayValue, false, false, false)
        };
}
