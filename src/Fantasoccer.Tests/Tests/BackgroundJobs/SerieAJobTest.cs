using Fantasoccer.Business;
using Fantasoccer.Engine;
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
    public async Task DoesNotOverwriteStoredCalendarWithEmptyResponse()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.InsertAsync(year, CalendarWithCompletedDay(), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        Assert.Single(calendar.Days);
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

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(year, TestContext.Current.CancellationToken);
        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        Assert.NotNull(rank);
        Assert.Equal("Roma", rank.Teams[0].Name);
        Assert.Equal(3, rank.Teams[0].Point);
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
        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(year, TestContext.Current.CancellationToken);
        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        Assert.Equal([1, 2], calendar.Days.Select(x => x.SerieADay));
        Assert.True(calendar.Days[0].Games.Single(x => x.Home.Name == "Roma").Delayed);
        Assert.Equal((1, 4), _serieA.CalendarRequest);
        Assert.NotNull(rank);
        Assert.Equal("Inter", rank.Teams[0].Name);
        Assert.Equal(3, rank.Teams[0].Point);
        Assert.Equal("Napoli", rank.Teams[1].Name);
    }

    [Fact]
    public async Task CalculatesAndOrdersRankFromCompletedGames()
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
                    Games =
                    [
                        Game("Roma", "Milan", delayed: false, 3, 1),
                        Game("Inter", "Juventus", delayed: false, 2, 0),
                        Game("Napoli", "Torino", delayed: false, 1, 0),
                        Game("Lazio", "Fiorentina", delayed: false, 1, 1),
                        Game("Bologna", "Parma", delayed: false, 4, 0, new DateTime(2027, 5, 30)),
                        Game("Genoa", "Como", delayed: true, 5, 0)
                    ]
                }
            ]
        };

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(rank);
        Assert.Equal(["Roma", "Inter", "Napoli"], rank.Teams.Take(3).Select(x => x.Name));
        var roma = rank.Teams.Single(x => x.Name == "Roma");
        Assert.Equal(3, roma.Point);
        Assert.Equal(1, roma.Victories);
        Assert.Equal(3, roma.Goal);
        Assert.Equal(1, roma.SufferedGoal);
        Assert.Equal(1, roma.Played);
        Assert.Equal(1, rank.Teams.Single(x => x.Name == "Lazio").Point);
        Assert.Equal(0, rank.Teams.Single(x => x.Name == "Bologna").Played);
        Assert.Equal(0, rank.Teams.Single(x => x.Name == "Genoa").Played);
    }

    [Fact]
    public async Task RecalculatesExistingRankWhenCalendarDoesNotChange()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.InsertAsync(year, new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    Year = year,
                    SerieADay = 1,
                    Games = [Game("Roma", "Milan", delayed: false, 2, 1)]
                }
            ]
        }, TestContext.Current.CancellationToken);
        await _ranks.InsertAsync(year, new RealRank
        {
            Teams = [new RealRankedTeam { Name = "Milan", Point = 99 }]
        }, TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var rank = await _ranks.GetAsync(year, TestContext.Current.CancellationToken);
        Assert.NotNull(rank);
        Assert.Equal("Roma", rank.Teams[0].Name);
        Assert.Equal(3, rank.Teams[0].Point);
        Assert.Equal(0, rank.Teams.Single(x => x.Name == "Milan").Point);
    }

    [Fact]
    public void BackgroundJobsRegisterSystemTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddFantasoccerBackgroundWork();
        using var provider = services.BuildServiceProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
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

    private static RealGame Game(
        string home,
        string away,
        bool delayed,
        int? homeGoal = 1,
        int? awayGoal = 0,
        DateTime? date = null)
        => new()
        {
            Home = new RealTeam { Name = home },
            Away = new RealTeam { Name = away },
            Date = date ?? new DateTime(2026, 8, 26, 12, 0, 0),
            Delayed = delayed,
            HomeGoal = homeGoal,
            AwayGoal = awayGoal
        };
}

public sealed class TestSerieAInformation : ISerieAInformation
{
    public RealCalendar Calendar { get; set; } = new() { Days = [] };
    public (int Min, int Max)? CalendarRequest { get; set; }

    public Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38)
    {
        CalendarRequest = (min, max);
        return Task.FromResult(Calendar);
    }
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
