using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class CalendarRulesTest
{
    private readonly IRandomCalendar _randomCalendar;
    private readonly ILeagueCalculator _league;

    public CalendarRulesTest(IRandomCalendar randomCalendar, IFactory<ILeagueCalculator> factory)
    {
        _randomCalendar = randomCalendar;
        _league = factory.Create(LeagueType.League.ToString())!;
    }

    [Theory]
    [InlineData(4, 3, 2, 6)]
    [InlineData(5, 5, 2, 10)]
    public void RoundRobinUsesEveryPairOnce(
        int teamCount,
        int expectedDays,
        int gamesPerDay,
        int expectedPairs)
    {
        var games = _randomCalendar.GetCalendar(teamCount, 1);

        Assert.Equal(expectedDays, games.Select(x => x.Day).Distinct().Count());
        Assert.All(games.GroupBy(x => x.Day), day =>
        {
            Assert.Equal(gamesPerDay, day.Count());
            Assert.Equal(gamesPerDay * 2, day.SelectMany(x => new[] { x.Game.ATeam, x.Game.BTeam }).Distinct().Count());
        });
        Assert.DoesNotContain(games, x => x.Game.ATeam == x.Game.BTeam);
        Assert.Equal(expectedPairs, games
            .Select(x => Pair(x.Game.ATeam, x.Game.BTeam))
            .Distinct()
            .Count());
        Assert.All(Enumerable.Range(0, teamCount), team =>
            Assert.Equal(teamCount - 1, games.Count(x => x.Game.ATeam == team || x.Game.BTeam == team)));
    }

    [Fact]
    public void LeagueCalendarAppliesDelayedAndCancelledDays()
    {
        var settings = LeagueSetting.Default;
        settings.DelayedDay = 2;
        settings.CancelledDay = 1;
        var teams = Enumerable.Range(1, 4)
            .Select(x => new AnnualTeam { Name = $"Team {x}", Owner = $"owner{x}", AdditionalOwners = [] })
            .ToList();

        var calendar = _league.GetRandomCalendar(2026, new AnnualLeague
        {
            Year = 2026,
            Settings = settings
        }, teams);

        var days = Assert.Single(calendar.Rounds).Value;
        Assert.Equal(36, days.Count);
        Assert.Equal((1, 3), (days[0].Number, days[0].SerieADay));
        Assert.Equal((36, 38), (days[^1].Number, days[^1].SerieADay));
        Assert.All(days, day => Assert.Equal(2, day.Games.Count));
        Assert.All(days[0].Games, game => Assert.True(game.Result?.IsCancelled));
        Assert.All(days.Skip(1).SelectMany(x => x.Games), game => Assert.Null(game.Result));
    }

    private static (int First, int Second) Pair(int first, int second)
        => first < second ? (first, second) : (second, first);
}
