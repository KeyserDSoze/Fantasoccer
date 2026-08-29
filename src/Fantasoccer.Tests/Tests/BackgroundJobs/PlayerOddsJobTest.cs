using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class PlayerOddsJobTest : IAsyncLifetime
{
    private readonly PlayerOddsJob _job;
    private readonly IFantasoccerLogger _logger;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private readonly IRepository<ChancedRealPlayerWrapper, YearDayKey> _odds;
    private RealCalendar? _calendarSnapshot;
    private RealPlayersWrapper? _playersSnapshot;
    private ChancedRealPlayerWrapper? _oddsSnapshot;

    public PlayerOddsJobTest(
        PlayerOddsJob job,
        IFantasoccerLogger logger,
        IRepository<RealCalendar, int> calendars,
        IRepository<RealPlayersWrapper, int> players,
        IRepository<ChancedRealPlayerWrapper, YearDayKey> odds)
    {
        _job = job;
        _logger = logger;
        _calendars = calendars;
        _players = players;
        _odds = odds;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _calendars.GetAsync(year, cancellationToken);
        _playersSnapshot = await _players.GetAsync(year, cancellationToken);
        _oddsSnapshot = await _odds.GetAsync(Key(1), cancellationToken);
        await _calendars.DeleteAsync(year, cancellationToken);
        await _players.DeleteAsync(year, cancellationToken);
        await _odds.DeleteAsync(Key(1), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        await _calendars.DeleteAsync(year, cancellationToken);
        await _players.DeleteAsync(year, cancellationToken);
        await _odds.DeleteAsync(Key(1), cancellationToken);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot, cancellationToken);
        if (_playersSnapshot != null)
            await _players.InsertAsync(year, _playersSnapshot, cancellationToken);
        if (_oddsSnapshot != null)
            await _odds.InsertAsync(Key(1), _oddsSnapshot, cancellationToken);
    }

    [Fact]
    public async Task DoesNothingWhenCalendarIsMissing()
    {
        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _calendars.GetAsync(FantacalcioTime.ActualYear, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoesNotCreateOddsWhenThereIsNoNextDay()
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days = []
        }, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await _odds.ToListAsEntityAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoesNotFailWhenRealPlayersAreMissing()
    {
        await InsertNextDayCalendarAsync();

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _odds.GetAsync(Key(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergesParserChancesIntoExistingPlayers()
    {
        await InsertNextDayCalendarAsync();
        await _players.InsertAsync(FantacalcioTime.ActualYear, new RealPlayersWrapper
        {
            Players = [CreatePlayer("Mario Rossi", "Roma")]
        }, TestContext.Current.CancellationToken);
        var factory = new TestFormationFactory
        {
            Parsers =
            {
                ["Fantagazzetta"] = new TestFormationParser([CreateChancedPlayer("Mario Rossi", "Roma", new Chance { Fantagazzetta = true })]),
                ["Gazzetta"] = new TestFormationParser([CreateChancedPlayer("Mario Rossi", "Roma", new Chance { Gazzetta = true })]),
                ["Injury"] = new TestFormationParser([CreateChancedPlayer("Mario Rossi", "Roma", new Chance { Status = ChanceType.Injury, Description = "Infortunato" })])
            }
        };

        await CreateJob(factory).ActionToDoAsync(TestContext.Current.CancellationToken);

        var wrapper = await _odds.GetAsync(Key(1), TestContext.Current.CancellationToken);
        Assert.NotNull(wrapper);
        var player = Assert.Single(wrapper.Players);
        Assert.True(player.Chance.Fantagazzetta);
        Assert.True(player.Chance.Gazzetta);
        Assert.Equal(ChanceType.Injury, player.Chance.Status);
        Assert.Equal("Infortunato", player.Chance.Description);
    }

    [Fact]
    public async Task InitializesExistingPlayerChanceWhenMissing()
    {
        await InsertNextDayCalendarAsync();
        await _odds.InsertAsync(Key(1), new ChancedRealPlayerWrapper
        {
            Players = [CreateChancedPlayer("Mario Rossi", "Roma", null)]
        }, TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var wrapper = await _odds.GetAsync(Key(1), TestContext.Current.CancellationToken);
        Assert.NotNull(wrapper);
        Assert.NotNull(Assert.Single(wrapper.Players).Chance);
    }

    [Fact]
    public async Task ContinuesWhenParserFails()
    {
        await InsertNextDayCalendarAsync();
        await _players.InsertAsync(FantacalcioTime.ActualYear, new RealPlayersWrapper
        {
            Players = [CreatePlayer("Mario Rossi", "Roma")]
        }, TestContext.Current.CancellationToken);
        var factory = new TestFormationFactory
        {
            Parsers =
            {
                ["Fantagazzetta"] = new TestFormationParser([CreateChancedPlayer("Mario Rossi", "Roma", new Chance { Fantagazzetta = true })]),
                ["Gazzetta"] = new TestFormationParser([], new InvalidOperationException("Parser unavailable")),
                ["Injury"] = new TestFormationParser([])
            }
        };

        await CreateJob(factory).ActionToDoAsync(TestContext.Current.CancellationToken);

        var wrapper = await _odds.GetAsync(Key(1), TestContext.Current.CancellationToken);
        Assert.NotNull(wrapper);
        Assert.True(Assert.Single(wrapper.Players).Chance.Fantagazzetta);
    }

    [Fact]
    public async Task ClearsStaleChancesWhenParsersReturnNoData()
    {
        await InsertNextDayCalendarAsync();
        await _odds.InsertAsync(Key(1), new ChancedRealPlayerWrapper
        {
            Players =
            [
                CreateChancedPlayer("Mario Rossi", "Roma", new Chance
                {
                    Fantagazzetta = true,
                    Gazzetta = true,
                    Mediaset = true,
                    Sky = true,
                    Status = ChanceType.Injury,
                    Description = "Infortunato"
                })
            ]
        }, TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var wrapper = await _odds.GetAsync(Key(1), TestContext.Current.CancellationToken);
        var chance = Assert.Single(wrapper!.Players).Chance;
        Assert.False(chance.Fantagazzetta);
        Assert.False(chance.Gazzetta);
        Assert.False(chance.Mediaset);
        Assert.False(chance.Sky);
        Assert.Equal(ChanceType.Normal, chance.Status);
        Assert.Null(chance.Description);
    }

    private PlayerOddsJob CreateJob(TestFormationFactory? factory = null)
        => new(_logger, factory ?? new TestFormationFactory(), _odds, _calendars, _players);

    private async Task InsertNextDayCalendarAsync()
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games =
                    [
                        new RealGame
                        {
                            Home = new RealTeam { Name = "Roma" },
                            Away = new RealTeam { Name = "Milan" },
                            Date = FantacalcioTime.ItalyNow.AddDays(1),
                            Delayed = false
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);
    }

    private static YearDayKey Key(int day)
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Day = day
        };

    private static RealPlayer CreatePlayer(string name, string teamName)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = teamName },
            Role = Role.Forward,
            IsActive = true,
            Visible = true
        };

    private static ChancedRealPlayer CreateChancedPlayer(string name, string teamName, Chance? chance)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = teamName },
            Role = Role.Forward,
            IsActive = true,
            Visible = true,
            Chance = chance!
        };
}

public sealed class TestFormationParser : IFormationParser
{
    private readonly List<ChancedRealPlayer> _players;
    private readonly Exception? _exception;

    public TestFormationParser(List<ChancedRealPlayer> players, Exception? exception = null)
    {
        _players = players;
        _exception = exception;
    }

    public Task<List<ChancedRealPlayer>> GetPlayersAsync()
        => _exception == null ? Task.FromResult(_players) : Task.FromException<List<ChancedRealPlayer>>(_exception);
}

public sealed class TestFormationFactory : IFactory<IFormationParser>
{
    public Dictionary<string, IFormationParser> Parsers { get; } = [];

    public IFormationParser? Create(AnyOf<string?, Enum>? name = null)
        => Parsers.GetValueOrDefault(name?.ToString() ?? string.Empty) ?? new TestFormationParser([]);

    public IFormationParser? CreateWithoutDecoration(AnyOf<string?, Enum>? name = null)
        => Create(name);

    public IEnumerable<IFormationParser> CreateAll(AnyOf<string?, Enum>? name = null)
        => Parsers.Values;

    public IEnumerable<IFormationParser> CreateAllWithoutDecoration(AnyOf<string?, Enum>? name = null)
        => CreateAll(name);

    public bool Exists(AnyOf<string?, Enum>? name = null)
        => Parsers.ContainsKey(name?.ToString() ?? string.Empty);
}
