using Fantasoccer.Business;
using Fantasoccer.Engine.IngestionData;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class SerieAJobTest : IAsyncLifetime
{
    private readonly ServiceProvider _services;
    private readonly TestSerieAInformation _serieA = new();
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<RealRank, int> _ranks;
    private RealCalendar? _calendarSnapshot;
    private RealRank? _rankSnapshot;

    public SerieAJobTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<RealRank, int>(builder => builder.WithInMemory());
        services.AddSharedBusiness();

        _services = services.BuildServiceProvider();
        _calendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
        _ranks = _services.GetRequiredService<IRepository<RealRank, int>>();
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _calendars.GetAsync(year, cancellationToken);
        _rankSnapshot = await _ranks.GetAsync(year, cancellationToken);
        await _calendars.DeleteAsync(year, cancellationToken);
        await _ranks.DeleteAsync(year, cancellationToken);
        _serieA.Calendar = new RealCalendar
        {
            Year = year,
            Days = [new RealDay { Year = year, SerieADay = 1, Games = [] }]
        };
        _serieA.Rank = new RealRank { Teams = [] };
        _serieA.CalendarRequest = null;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        await _calendars.DeleteAsync(year, cancellationToken);
        await _ranks.DeleteAsync(year, cancellationToken);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot, cancellationToken);
        if (_rankSnapshot != null)
            await _ranks.InsertAsync(year, _rankSnapshot, cancellationToken);

        _services.Dispose();
    }

    [Fact]
    public async Task DoesNotOverwriteStoredDataWithEmptyResponses()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.InsertAsync(year, CalendarWithCompletedDay(), TestContext.Current.CancellationToken);
        await _ranks.InsertAsync(year, new RealRank
        {
            Teams = [new RealRankedTeam { Name = "Roma", Point = 3 }]
        }, TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(year, TestContext.Current.CancellationToken);
        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        Assert.Single(calendar.Days);
        Assert.NotNull(rank);
        Assert.Equal(3, Assert.Single(rank.Teams).Point);
    }

    [Fact]
    public async Task CreatesCalendarAndRankWhenStorageIsEmpty()
    {
        var year = FantacalcioTime.ActualYear;
        _serieA.Calendar = new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    Year = year,
                    SerieADay = 1,
                    Games = [Game("Roma", "Milan", delayed: false)]
                }
            ]
        };
        _serieA.Rank = new RealRank
        {
            Teams = [new RealRankedTeam { Name = "Roma", Point = 3 }]
        };

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(await _calendars.GetAsync(year, TestContext.Current.CancellationToken));
        Assert.NotNull(await _ranks.GetAsync(year, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergesPartialCalendarAndPreservesDelayedGames()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.InsertAsync(year, CalendarWithCompletedDay(), TestContext.Current.CancellationToken);
        _serieA.Calendar = new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    Year = year,
                    SerieADay = 1,
                    Games =
                    [
                        Game("Roma", "Milan", delayed: false),
                        Game("Inter", "Juventus", delayed: false)
                    ]
                },
                new RealDay
                {
                    Year = year,
                    SerieADay = 2,
                    Games = [Game("Napoli", "Lazio", delayed: false)]
                }
            ]
        };
        _serieA.Rank = new RealRank
        {
            Teams = [new RealRankedTeam { Name = "Inter", Point = 6 }]
        };

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(year, TestContext.Current.CancellationToken);
        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        Assert.Equal([1, 2], calendar.Days.Select(x => x.SerieADay));
        Assert.True(calendar.Days[0].Games.Single(x => x.Home.Name == "Roma").Delayed);
        Assert.Equal((1, 4), _serieA.CalendarRequest);
        Assert.NotNull(rank);
        Assert.Equal("Inter", Assert.Single(rank.Teams).Name);
    }

    private SerieAJob CreateJob()
        => new(
            _services.GetRequiredService<IFantasoccerLogger>(),
            _serieA,
            _calendars,
            _ranks,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero)));

    private static RealCalendar CalendarWithCompletedDay()
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    Year = FantacalcioTime.ActualYear,
                    SerieADay = 1,
                    Games =
                    [
                        Game("Roma", "Milan", delayed: true),
                        Game("Inter", "Juventus", delayed: false)
                    ]
                }
            ]
        };

    private static RealGame Game(string home, string away, bool delayed)
        => new()
        {
            Home = new RealTeam { Name = home },
            Away = new RealTeam { Name = away },
            Date = DateTime.UtcNow.AddDays(-1),
            Delayed = delayed
        };
}

public sealed class TestSerieAInformation : ISerieAInformation
{
    public RealCalendar Calendar { get; set; } = new() { Days = [] };
    public RealRank Rank { get; set; } = new() { Teams = [] };
    public (int Min, int Max)? CalendarRequest { get; set; }

    public Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38)
    {
        CalendarRequest = (min, max);
        return Task.FromResult(Calendar);
    }

    public Task<RealRank> GetRankAsync()
        => Task.FromResult(Rank);
}

public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
        => _utcNow;
}
