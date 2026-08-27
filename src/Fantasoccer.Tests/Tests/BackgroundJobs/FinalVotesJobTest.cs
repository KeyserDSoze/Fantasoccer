using Fantasoccer.Engine.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class FinalVotesJobTest : IAsyncLifetime
{
    private readonly FinalVotesJob _job;
    private readonly FinalVotesTestVoteGenerator _votes = new();
    private readonly FinalVotesTestStatsGenerator _stats = new();
    private readonly IRepository<RealCalendar, int> _calendars;
    private RealCalendar? _calendarSnapshot;

    public FinalVotesJobTest(
        IFantasoccerLogger logger,
        IRepository<RealCalendar, int> calendars)
    {
        _calendars = calendars;
        _job = new FinalVotesJob(logger, calendars, _votes, _stats);
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _calendars.GetAsync(year, cancellationToken);
        await _calendars.DeleteAsync(year, cancellationToken);
        _votes.Result = false;
        _votes.Request = null;
        _stats.Request = null;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        await _calendars.DeleteAsync(year, cancellationToken);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot, cancellationToken);
    }

    [Fact]
    public async Task DoesNothingWhenCalendarIsMissing()
    {
        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(_votes.Request);
        Assert.Null(_stats.Request);
    }

    [Fact]
    public async Task DoesNothingWhenCalendarHasNoCompletedOrLiveDay()
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games = [Game(FantacalcioTime.ItalyNow.AddHours(3))]
                }
            ]
        }, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(_votes.Request);
        Assert.Null(_stats.Request);
    }

    [Fact]
    public async Task DoesNotRegenerateStatsWhenVotesAreUnchanged()
    {
        await InsertCompletedDayAsync(7, dayYear: 0);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((FantacalcioTime.ActualYear, 7, false, 7), _votes.Request);
        Assert.Null(_stats.Request);
    }

    [Fact]
    public async Task RegeneratesStatsWithActualYearWhenVotesChange()
    {
        _votes.Result = true;
        await InsertCompletedDayAsync(7, dayYear: 0);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((FantacalcioTime.ActualYear, 7), _stats.Request);
    }

    [Fact]
    public async Task PrefersLiveDayOverLastCompletedDay()
    {
        _votes.Result = true;
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 7,
                    Games = [Game(FantacalcioTime.ItalyNow.AddHours(-12))]
                },
                new RealDay
                {
                    SerieADay = 8,
                    Games = [Game(FantacalcioTime.ItalyNow.AddMinutes(-10))]
                }
            ]
        }, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((FantacalcioTime.ActualYear, 8, false, 8), _votes.Request);
        Assert.Equal((FantacalcioTime.ActualYear, 8), _stats.Request);
    }

    private async Task InsertCompletedDayAsync(int day, int dayYear)
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    Year = dayYear,
                    SerieADay = day,
                    Games = [Game(FantacalcioTime.ItalyNow.AddHours(-3))]
                }
            ]
        }, TestContext.Current.CancellationToken);
    }

    private static RealGame Game(DateTime date)
        => new()
        {
            Home = new RealTeam { Name = "Roma" },
            Away = new RealTeam { Name = "Milan" },
            Date = date,
            Delayed = false
        };
}

public sealed class FinalVotesTestVoteGenerator : IVotePlayerGenerator
{
    public bool Result { get; set; }
    public (int Year, int Day, bool Force, int WithMax)? Request { get; set; }

    public ValueTask<bool> GenerateAndSaveAsync(int year, int untilThisDay, bool force = false, int withMax = 1)
    {
        Request = (year, untilThisDay, force, withMax);
        return ValueTask.FromResult(Result);
    }
}

public sealed class FinalVotesTestStatsGenerator : IStatsPlayerGenerator
{
    public (int Year, int Day)? Request { get; set; }

    public ValueTask GenerateAndSaveAsync(int year, int untilThisDay)
    {
        Request = (year, untilThisDay);
        return ValueTask.CompletedTask;
    }
}
