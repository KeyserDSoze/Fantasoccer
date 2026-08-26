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
}