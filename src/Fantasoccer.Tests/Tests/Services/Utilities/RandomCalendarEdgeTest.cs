using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Services.Utilities;

[Collection("InMemoryRepository")]
public sealed class RandomCalendarEdgeTest
{
    private readonly IRandomCalendar _calendar;

    public RandomCalendarEdgeTest(IRandomCalendar calendar)
    {
        _calendar = calendar;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Does_not_create_matches_for_fewer_than_two_teams(int teamCount)
    {
        Assert.Empty(_calendar.GetCalendar(teamCount, 1));
    }

    [Fact]
    public void Odd_team_count_gives_each_team_one_bye()
    {
        var games = _calendar.GetCalendar(5, 1).Select(x => x.Game).ToList();

        Assert.Equal(10, games.Count);
        Assert.Equal(10, games.Select(x => string.Join('-', new[] { x.ATeam, x.BTeam }.Order())).Distinct().Count());
    }
}
