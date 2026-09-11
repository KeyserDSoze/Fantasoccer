namespace Fantasoccer.Tests.Tests.Services;

public sealed class RealCalendarTest
{
    [Fact]
    public void LastDayIsNullForEmptyCalendar()
    {
        var calendar = new RealCalendar { Days = [] };

        Assert.Null(calendar.LastDay);
    }

    [Fact]
    public void LastDayIgnoresDayWithOnlyDelayedGames()
    {
        var calendar = new RealCalendar
        {
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games = [new RealGame { Delayed = true }]
                }
            ]
        };

        Assert.Null(calendar.LastDay);
    }

    [Fact]
    public void LastDayReturnsCompletedDayWithNonDelayedGame()
    {
        var calendar = new RealCalendar
        {
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games =
                    [
                        new RealGame
                        {
                            Date = FantacalcioTime.ItalyNow.AddHours(-3),
                            Delayed = false
                        }
                    ]
                }
            ]
        };

        Assert.Equal(1, calendar.LastDay?.SerieADay);
    }

    [Fact]
    public void LiveDayIgnoresEmptyDays()
    {
        var calendar = new RealCalendar
        {
            Days = [new RealDay { SerieADay = 1, Games = [] }]
        };

        Assert.Null(calendar.LiveDay);
        Assert.Equal(1, calendar.NextDay?.SerieADay);
    }

    [Fact]
    public void LiveDayIgnoresDaysWithOnlyDelayedGames()
    {
        var calendar = new RealCalendar
        {
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games =
                    [
                        new RealGame
                        {
                            Date = FantacalcioTime.ItalyNow,
                            Delayed = true
                        }
                    ]
                }
            ]
        };

        Assert.Null(calendar.LiveDay);
        Assert.Null(calendar.LiveGames);
    }

    [Fact]
    public void LiveFormationWindowEndsFourHoursAfterLastGameStarts()
    {
        var lastGameStart = FantacalcioTime.ItalyNow.AddHours(-1);
        var calendar = new RealCalendar
        {
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games =
                    [
                        new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-2) },
                        new RealGame { Date = lastGameStart }
                    ]
                }
            ]
        };

        Assert.Equal(lastGameStart.AddHours(4), calendar.LiveFormationDeadline);
        Assert.True(calendar.IsLiveFormationWindowOpen);
    }

    [Fact]
    public void LiveFormationWindowClosesAfterDeadline()
    {
        var calendar = new RealCalendar
        {
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-5) }]
                }
            ]
        };

        Assert.NotNull(calendar.LiveDay);
        Assert.False(calendar.IsLiveFormationWindowOpen);
    }
}
