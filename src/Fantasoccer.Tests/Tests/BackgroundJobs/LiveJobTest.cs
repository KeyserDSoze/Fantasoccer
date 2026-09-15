using Fantasoccer.Business;
using Fantasoccer.Engine.IngestionData;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class LiveJobTest : IAsyncLifetime
{
    private const string GroupId = "live-job-test";
    private const string LeagueId = "live-league-test";
    private const string BasketId = "live-basket-test";
    private const string HomeOwner = "home@test.local";
    private const string AwayOwner = "away@test.local";
    private readonly ServiceProvider _services;
    private readonly TestLiveTeamCalculator _teamCalculator = new();
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<LiveGroup, string> _liveGroups;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<Rank, LeagueKey> _ranks;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private readonly IRepository<RealCalendar, int> _realCalendars;
    private readonly List<Group> _groupSnapshot = [];
    private LiveGroup? _liveGroupSnapshot;
    private RealCalendar? _realCalendarSnapshot;

    public LiveJobTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<LiveGroup, string>(builder => builder.WithInMemory());
        services.AddSharedBusiness();

        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _liveGroups = _services.GetRequiredService<IRepository<LiveGroup, string>>();
        _calendars = _services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _ranks = _services.GetRequiredService<IRepository<Rank, LeagueKey>>();
        _teams = _services.GetRequiredService<IRepository<Team, TeamDayKey>>();
        _realCalendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _groupSnapshot.AddRange(await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken));
        foreach (var group in _groupSnapshot)
            await _groups.DeleteAsync(group.Id, cancellationToken);
        _liveGroupSnapshot = await _liveGroups.GetAsync(GroupId, cancellationToken);
        await _liveGroups.DeleteAsync(GroupId, cancellationToken);

        _realCalendarSnapshot = await _realCalendars.GetAsync(FantacalcioTime.ActualYear, cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        await CleanupLeagueAsync(cancellationToken);
        _teamCalculator.BuildRequest = null;
        _teamCalculator.SettingsChanges = 0;
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _liveGroups.DeleteAsync(GroupId, cancellationToken);
        await CleanupLeagueAsync(cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        foreach (var group in _groupSnapshot)
            await _groups.InsertAsync(group.Id, group, cancellationToken);
        if (_liveGroupSnapshot != null)
            await _liveGroups.InsertAsync(GroupId, _liveGroupSnapshot, cancellationToken);
        if (_realCalendarSnapshot != null)
            await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, _realCalendarSnapshot, cancellationToken);

    }

    [Fact]
    public async Task DoesNothingWhenRealCalendarIsMissing()
    {
        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _liveGroups.GetAsync(GroupId, TestContext.Current.CancellationToken));
        Assert.Null(_teamCalculator.BuildRequest);
    }

    [Fact]
    public async Task HandlesGroupWithoutLeagues()
    {
        await InsertLiveCalendarAsync();
        await _groups.InsertAsync(GroupId, new Group
        {
            Id = GroupId,
            Name = "Live group",
            Leagues = null!,
            Users = [],
            Baskets = []
        }, TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var liveGroup = await _liveGroups.GetAsync(GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(liveGroup);
        Assert.Empty(liveGroup.Leagues);
    }

    [Fact]
    public async Task BuildsLiveResultsAndRank()
    {
        var year = FantacalcioTime.ActualYear;
        await InsertLiveCalendarAsync();
        await _groups.InsertAsync(GroupId, CreateGroup(year), TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(LeagueKey(), CreateLeagueCalendar(), TestContext.Current.CancellationToken);
        await _ranks.InsertAsync(LeagueKey(), CreateRank(), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), CreateTeam(HomeOwner, "Home player"), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner), CreateTeam(AwayOwner, "Away player"), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var liveGroup = await _liveGroups.GetAsync(GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(liveGroup);
        var liveLeague = Assert.Single(liveGroup.Leagues);
        var game = Assert.Single(liveLeague.Rounds["Regular"].Games);
        Assert.Equal(72, game.Result?.Home.Value);
        Assert.Equal(66, game.Result?.Away.Value);
        Assert.Equal(2, game.Result?.HomeGoal);
        Assert.Equal(1, game.Result?.AwayGoal);
        Assert.Equal(3, liveLeague.Rank.RankedTeams["Regular"].Single(x => x.Owner == HomeOwner).Point);
        Assert.Equal((year, 1), _teamCalculator.BuildRequest);
        Assert.Equal(1, _teamCalculator.SettingsChanges);
    }

    [Fact]
    public async Task UsesNextDayWhenNoDayIsLive()
    {
        var year = FantacalcioTime.ActualYear;
        await _realCalendars.InsertAsync(year, new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    Year = year,
                    SerieADay = 7,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-12), Delayed = false }]
                },
                new RealDay
                {
                    Year = year,
                    SerieADay = 8,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(3), Delayed = false }]
                }
            ]
        }, TestContext.Current.CancellationToken);
        await _groups.InsertAsync(GroupId, CreateGroup(year), TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(LeagueKey(), CreateLeagueCalendar(8), TestContext.Current.CancellationToken);
        await _ranks.InsertAsync(LeagueKey(), CreateRank(), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner, 8), CreateTeam(HomeOwner, "Home player"), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner, 8), CreateTeam(AwayOwner, "Away player"), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((year, 8), _teamCalculator.BuildRequest);
        var liveGroup = await _liveGroups.GetAsync(GroupId, TestContext.Current.CancellationToken);
        Assert.NotNull(liveGroup);
        Assert.Equal(8, liveGroup.Leagues.Single().Rounds["Regular"].SerieADay);
    }

    private LiveJob CreateJob()
        => new(
            _services.GetRequiredService<IFantasoccerLogger>(),
            _teamCalculator,
            _services.GetRequiredService<IRankCalculator>(),
            _groups,
            _liveGroups,
            _calendars,
            _ranks,
            _teams,
            _realCalendars);

    private async Task InsertLiveCalendarAsync()
    {
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
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
                        new RealGame
                        {
                            Home = new RealTeam { Name = "Roma" },
                            Away = new RealTeam { Name = "Milan" },
                            Date = FantacalcioTime.ItalyNow.AddMinutes(-10)
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);
    }

    private static Group CreateGroup(int year)
        => new()
        {
            Id = GroupId,
            Name = "Live group",
            Users = [],
            Leagues =
            [
                new League
                {
                    Id = LeagueId,
                    Name = "Live league",
                    Type = LeagueType.League,
                    BasketsId = [BasketId],
                    Years = [new AnnualLeague { Year = year, Settings = LeagueSetting.Default }]
                }
            ],
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Live basket",
                    Years =
                    [
                        new YearlyBasket
                        {
                            Year = year,
                            Teams =
                            [
                                new AnnualTeam { Name = "Home", Owner = HomeOwner, AdditionalOwners = [] },
                                new AnnualTeam { Name = "Away", Owner = AwayOwner, AdditionalOwners = [] }
                            ]
                        }
                    ]
                }
            ]
        };

    private static Calendar CreateLeagueCalendar(int day = 1)
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["Regular"] =
                [
                    new Day
                    {
                        SerieADay = day,
                        Number = day,
                        Games =
                        [
                            new CalendarGame
                            {
                                Id = "game-1",
                                Home = "Home",
                                HomeOwner = HomeOwner,
                                Away = "Away",
                                AwayOwner = AwayOwner
                            }
                        ]
                    }
                ]
            }
        };

    private static Rank CreateRank()
        => new()
        {
            SerieADay = 0,
            RankedTeams = new Dictionary<string, List<RankedTeam>>
            {
                ["Regular"] =
                [
                    new RankedTeam { Name = "Home", Owner = HomeOwner },
                    new RankedTeam { Name = "Away", Owner = AwayOwner }
                ]
            }
        };

    private static Team CreateTeam(string owner, string playerName)
        => new()
        {
            Name = owner,
            Owner = owner,
            AdditionalOwners = [],
            Players =
            [
                new Player
                {
                    Name = playerName,
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    Status = PlayerInTeamStatus.Active
                }
            ]
        };

    private static LeagueKey LeagueKey()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear };

    private static TeamDayKey TeamKey(string owner, int day = 1)
        => new()
        {
            Group = GroupId,
            Year = FantacalcioTime.ActualYear,
            BasketId = BasketId,
            Day = day,
            Email = owner
        };

    private async Task CleanupLeagueAsync(CancellationToken cancellationToken)
    {
        await _calendars.DeleteAsync(LeagueKey(), cancellationToken);
        await _ranks.DeleteAsync(LeagueKey(), cancellationToken);
        await _teams.DeleteAsync(TeamKey(HomeOwner), cancellationToken);
        await _teams.DeleteAsync(TeamKey(AwayOwner), cancellationToken);
        await _teams.DeleteAsync(TeamKey(HomeOwner, 8), cancellationToken);
        await _teams.DeleteAsync(TeamKey(AwayOwner, 8), cancellationToken);
    }
}

public sealed class TestLiveTeamCalculator : ITeamCalculator
{
    public (int Year, int Day)? BuildRequest { get; set; }
    public int SettingsChanges { get; set; }
    public bool IsLiveResult { get; set; } = true;
    public VotedRealPlayerWrapper? OfficialResult { get; set; }
    public bool IsLive => IsLiveResult;
    public bool IsEditable => false;
    public int SerieADay => BuildRequest?.Day ?? 0;
    public VotedRealPlayerWrapper? Live => null;
    public VotedRealPlayerWrapper? Official => OfficialResult;
    public ChancedRealPlayerWrapper? Chance => null;
    public RealDay? RealDay => null;
    public LeagueSetting? LeagueSetting => global::Fantasoccer.LeagueSetting.Default;
    public LeagueType LeagueType => LeagueType.League;

    public ValueTask BuildRequestAsync(int year, int day, LeagueType leagueType, LeagueSetting leagueSettings, bool withRealGame, bool withVote, bool withChance)
    {
        BuildRequest = (year, day);
        return ValueTask.CompletedTask;
    }

    public void SetLeagueSettings(LeagueType leagueType, LeagueSetting leagueSettings)
        => SettingsChanges++;

    public Point CalculatePoint(IEnumerable<Player> players)
        => new(players.First().Name == "Home player" ? 72 : 66, false, false, false);

    public Point CalculatePoint(IEnumerable<EnrichedPlayer> players)
        => Point.Zero;

    public EnrichedPlayer Enrich(Player player)
        => throw new NotSupportedException();

    public EnrichedPlayer ForceEnrich(Player player, Vote? vote, Chance? chance, RealGame? realGame)
        => throw new NotSupportedException();

    public IEnumerable<EnrichedPlayer> Enrich(IEnumerable<Player> players)
        => throw new NotSupportedException();

    public VoteValue FinalValue(Player player)
        => VoteValue.Empty;

    public VoteValue FinalValue(EnrichedPlayer player)
        => VoteValue.Empty;

    public ValueTask<bool> RecalculateDayAsync(int serieADay)
        => ValueTask.FromResult(false);
}
