using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class AllPlayersAndAllTeamsJobTest : IAsyncLifetime
{
    private readonly AllPlayersAndAllTeamsJob _job;
    private readonly TestOfficialVote _officialVote;
    private readonly TestAllPlayers _allPlayers;
    private readonly TestStatsPlayerGenerator _stats;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private readonly IRepository<RealTeamWrapper, int> _teams;
    private readonly IRepository<RealCalendar, int> _calendars;
    private RealPlayersWrapper? _playersSnapshot;
    private RealTeamWrapper? _teamsSnapshot;
    private RealCalendar? _calendarSnapshot;

    public AllPlayersAndAllTeamsJobTest(
        AllPlayersAndAllTeamsJob job,
        TestOfficialVote officialVote,
        TestAllPlayers allPlayers,
        TestStatsPlayerGenerator stats,
        IRepository<RealPlayersWrapper, int> players,
        IRepository<RealTeamWrapper, int> teams,
        IRepository<RealCalendar, int> calendars)
    {
        _job = job;
        _officialVote = officialVote;
        _allPlayers = allPlayers;
        _stats = stats;
        _players = players;
        _teams = teams;
        _calendars = calendars;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _playersSnapshot = await _players.GetAsync(year);
        _teamsSnapshot = await _teams.GetAsync(year);
        _calendarSnapshot = await _calendars.GetAsync(year);
        await _players.DeleteAsync(year);
        await _teams.DeleteAsync(year);
        await _calendars.DeleteAsync(year);
        _officialVote.Players = [];
        _allPlayers.Players = [];
        _stats.LastRequest = null;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        await _players.DeleteAsync(year);
        await _teams.DeleteAsync(year);
        await _calendars.DeleteAsync(year);
        if (_playersSnapshot != null)
            await _players.InsertAsync(year, _playersSnapshot);
        if (_teamsSnapshot != null)
            await _teams.InsertAsync(year, _teamsSnapshot);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot);
    }

    [Fact]
    public async Task DoesNotFailWhenOfficialVotesAreUnavailable()
    {
        _officialVote.Players = null;
        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatesRealTeamsAndPlayersFromOfficialData()
    {
        var year = FantacalcioTime.ActualYear;
        var teamName = $"Team-{Guid.NewGuid():N}";
        _officialVote.Players = [CreateVotedPlayer("New player", teamName)];
        _allPlayers.Players = [CreatePlayer("New player", teamName)];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var teams = await _teams.GetAsync(year);
        var players = await _players.GetAsync(year);
        Assert.NotNull(teams);
        Assert.Contains(teams.Teams, x => x.Name == teamName);
        Assert.NotNull(players);
        Assert.Contains(players.Players, x => x.Name == "New player");
    }

    [Fact]
    public async Task PreservesMissingPlayersAsInactiveAndRegeneratesStatistics()
    {
        var year = FantacalcioTime.ActualYear;
        var existingPlayer = CreatePlayer("Existing player", "Roma");
        await _teams.InsertAsync(year, new RealTeamWrapper { Teams = [new RealTeam { Name = "Roma" }] });
        await _players.InsertAsync(year, new RealPlayersWrapper { Players = [existingPlayer] });
        _allPlayers.Players = [CreatePlayer("New player", "Milan")];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var players = await _players.GetAsync(year);
        Assert.NotNull(players);
        Assert.False(Assert.Single(players.Players, x => x.Name == "Existing player").IsActive);
        Assert.Contains(players.Players, x => x.Name == "New player" && x.IsActive);
        Assert.Equal((year, 38), _stats.LastRequest);
    }

    [Fact]
    public async Task UsesCalendarLastDayWhenRegeneratingStatistics()
    {
        var year = FantacalcioTime.ActualYear;
        await _teams.InsertAsync(year, new RealTeamWrapper { Teams = [new RealTeam { Name = "Roma" }] });
        await _players.InsertAsync(year, new RealPlayersWrapper { Players = [] });
        await _calendars.InsertAsync(year, new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    SerieADay = 12,
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
        });
        _allPlayers.Players = [CreatePlayer("New player", "Milan")];

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((year, 12), _stats.LastRequest);
    }

    private static RealPlayer CreatePlayer(string name, string teamName)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = teamName },
            Role = Role.Forward,
            IsActive = true
        };

    private static VotedRealPlayer CreateVotedPlayer(string name, string teamName)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = teamName },
            Role = Role.Forward,
            IsActive = true,
            Vote = Vote.Default(Role.Forward)
        };
}

public sealed class TestOfficialVote : IOfficialVote
{
    public List<VotedRealPlayer>? Players { get; set; } = [];

    public Task<List<VotedRealPlayer>> GetAsync(int year, int serieADay)
        => Task.FromResult(Players ?? []);
}

public sealed class TestAllPlayers : IAllPlayers
{
    public List<RealPlayer> Players { get; set; } = [];

    public Task<List<RealPlayer>> GetAsync()
        => Task.FromResult(Players);
}

public sealed class TestStatsPlayerGenerator : IStatsPlayerGenerator
{
    public (int Year, int Day)? LastRequest { get; set; }

    public ValueTask GenerateAndSaveAsync(int year, int untilThisDay)
    {
        LastRequest = (year, untilThisDay);
        return ValueTask.CompletedTask;
    }
}