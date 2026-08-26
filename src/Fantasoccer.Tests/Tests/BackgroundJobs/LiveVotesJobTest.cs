using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

public sealed class LiveVotesJobTest : IAsyncLifetime
{
    private readonly LiveVotesJob _job;
    private readonly TestLiveVote _liveVote;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _votes;
    private RealCalendar? _calendarSnapshot;

    public LiveVotesJobTest(
        LiveVotesJob job,
        TestLiveVote liveVote,
        IRepository<RealCalendar, int> calendars,
        IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory)
    {
        _job = job;
        _liveVote = liveVote;
        _calendars = calendars;
        _votes = factory.Create("live");
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _calendarSnapshot = await _calendars.GetAsync(year);
        await _calendars.DeleteAsync(year);
        _liveVote.Players = [];
        _liveVote.Calls = 0;
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

        Assert.Equal(0, _liveVote.Calls);
    }

    [Fact]
    public async Task DoesNothingWhenThereIsNoLiveDay()
    {
        var day = RandomDay();
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(3)));

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _liveVote.Calls);
    }

    [Fact]
    public async Task DoesNotPersistEmptyLiveVoteResponse()
    {
        var day = RandomDay();
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(-1)));

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _liveVote.Calls);
        Assert.Null(await _votes.GetAsync(Key(day)));
    }

    [Fact]
    public async Task UpdatesExistingPlayersAndAddsNewPlayers()
    {
        var day = RandomDay();
        var existing = CreatePlayer("Existing", 4);
        var added = CreatePlayer("Added", 7);
        var key = Key(day);
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(-1)));
        await _votes.InsertAsync(key, new VotedRealPlayerWrapper { Players = [CreatePlayer("Existing", 1)] });
        _liveVote.Players = [existing, added];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var result = await _votes.GetAsync(key);
        Assert.NotNull(result);
        Assert.Equal(2, result.Players.Count);
        Assert.Equal(4, result.Players.Single(x => x.Name == "Existing").Vote.Value);
        Assert.Equal(7, result.Players.Single(x => x.Name == "Added").Vote.Value);
    }

    private static int RandomDay()
        => Random.Shared.Next(1, 39);

    private static YearDayKey Key(int day)
        => new() { Year = FantacalcioTime.ActualYear, Day = day };

    private static RealCalendar CreateCalendar(int day, DateTime date)
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = day,
                    Games = [new RealGame { Date = date, Delayed = false }]
                }
            ]
        };

    private static VotedRealPlayer CreatePlayer(string name, float value)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = "Team" },
            Role = Role.Forward,
            IsActive = true,
            Vote = new Vote { Role = Role.Forward, Value = value }
        };
}

public sealed class TestLiveVote : ILiveVote
{
    public List<VotedRealPlayer> Players { get; set; } = [];
    public int Calls { get; set; }

    public Task<List<VotedRealPlayer>> GetAsync(int year, int day)
    {
        Calls++;
        return Task.FromResult(Players);
    }
}