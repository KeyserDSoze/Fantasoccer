using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class LiveVotesJobTest : IAsyncLifetime
{
    private readonly LiveVotesJob _job;
    private readonly TestLiveVote _liveVote;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _votes;
    private readonly Dictionary<int, VotedRealPlayerWrapper?> _voteSnapshots = [];
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
        _votes = factory.Create("live")!;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _calendars.GetAsync(year, cancellationToken);
        await _calendars.DeleteAsync(year, cancellationToken);
        foreach (var day in new[] { 2, 3, 4, 5 })
        {
            _voteSnapshots[day] = await _votes.GetAsync(Key(day), cancellationToken);
            await _votes.DeleteAsync(Key(day), cancellationToken);
        }
        _liveVote.Players = [];
        _liveVote.Calls = 0;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        await _calendars.DeleteAsync(year, cancellationToken);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot, cancellationToken);
        foreach (var snapshot in _voteSnapshots)
        {
            await _votes.DeleteAsync(Key(snapshot.Key), cancellationToken);
            if (snapshot.Value != null)
                await _votes.InsertAsync(Key(snapshot.Key), snapshot.Value, cancellationToken);
        }
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
        const int day = 2;
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(3)), TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _liveVote.Calls);
    }

    [Fact]
    public async Task DoesNotPersistEmptyLiveVoteResponse()
    {
        const int day = 3;
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(-1)), TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _liveVote.Calls);
        Assert.Null(await _votes.GetAsync(Key(day), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdatesExistingPlayersAndAddsNewPlayers()
    {
        const int day = 4;
        var existing = CreatePlayer("Existing", 4);
        var added = CreatePlayer("Added", 7);
        var key = Key(day);
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(-1)), TestContext.Current.CancellationToken);
        await _votes.InsertAsync(key, new VotedRealPlayerWrapper { Players = [CreatePlayer("Existing", 1)] }, TestContext.Current.CancellationToken);
        _liveVote.Players = [existing, added];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var result = await _votes.GetAsync(key, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(2, result.Players.Count);
        Assert.Equal(4, result.Players.Single(x => x.Name == "Existing").Vote.Value);
        Assert.Equal(7, result.Players.Single(x => x.Name == "Added").Vote.Value);
    }

    [Fact]
    public async Task PersistsPlayersWhenLiveVoteStoreIsMissing()
    {
        const int day = 5;
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, CreateCalendar(day, FantacalcioTime.ItalyNow.AddHours(-1)), TestContext.Current.CancellationToken);
        _liveVote.Players = [CreatePlayer("First live player", 7)];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var result = await _votes.GetAsync(Key(day), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(7, Assert.Single(result.Players).Vote.Value);
    }

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
