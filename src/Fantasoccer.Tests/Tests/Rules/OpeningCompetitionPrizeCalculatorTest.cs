using Fantasoccer.Business.Services;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class OpeningCompetitionPrizeCalculatorTest
{
    [Fact]
    public void SharedPositionReceivesTheSamePrize()
    {
        var rules = new[]
        {
            new OpeningCompetitionPrizeRule(1, 50),
            new OpeningCompetitionPrizeRule(2, 30),
            new OpeningCompetitionPrizeRule(3, 20)
        };
        var standings = new[] { ("a", 10f), ("b", 10f), ("c", 8f), ("d", 7f) };

        var result = OpeningCompetitionPrizeCalculator.Calculate(rules, standings);

        Assert.Equal(50, result["a"]);
        Assert.Equal(50, result["b"]);
        Assert.Equal(20, result["c"]);
        Assert.Equal(0, result["d"]);
    }

    [Fact]
    public void GetPrizeUsesTheSameSharedPositionRule()
    {
        var rules = new[] { new OpeningCompetitionPrizeRule(1, 50), new OpeningCompetitionPrizeRule(2, 30) };
        var standings = new[] { ("a", 10f), ("b", 10f), ("c", 8f) };

        Assert.Equal(50, OpeningCompetitionPrizeCalculator.GetPrize(rules, 10, standings));
    }
}
