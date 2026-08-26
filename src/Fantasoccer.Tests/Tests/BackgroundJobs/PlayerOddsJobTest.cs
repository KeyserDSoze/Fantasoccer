using Fantasoccer.Engine.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

public sealed class PlayerOddsJobTest : IAsyncLifetime
{
    private readonly PlayerOddsJob _job;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<ChancedRealPlayerWrapper, YearDayKey> _odds;
    private RealCalendar? _calendarSnapshot;

    public PlayerOddsJobTest(
        PlayerOddsJob job,
        IRepository<RealCalendar, int> calendars,
        IRepository<ChancedRealPlayerWrapper, YearDayKey> odds)
    {
        _job = job;
        _calendars = calendars;
        _odds = odds;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _calendarSnapshot = await _calendars.GetAsync(year);
        await _calendars.DeleteAsync(year);
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.DeleteAsync(year);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot);
    }

    [Fact]
    public async Task DoesNothingWhenCalendarIsMissing()
    {
        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _calendars.GetAsync(FantacalcioTime.ActualYear));
    }

    [Fact]
    public async Task DoesNotCreateOddsWhenThereIsNoNextDay()
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days = []
        });

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await _odds.ToListAsEntityAsync());
    }
}