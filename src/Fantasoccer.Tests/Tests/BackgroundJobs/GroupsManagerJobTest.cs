using Fantasoccer.Business;
using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class GroupsManagerJobTest : IAsyncLifetime
{
    private const string GroupId = "groups-manager-job-test";
    private const string LeagueId = "groups-manager-league-test";
    private const string BasketId = "groups-manager-basket-test";
    private const string HomeOwner = "groups-home@test.local";
    private const string AwayOwner = "groups-away@test.local";
    private readonly ServiceProvider _services;
    private readonly TestLiveTeamCalculator _calculator = new();
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<Rank, LeagueKey> _ranks;
    private readonly IRepository<Rank, YearDayLeague> _dailyRanks;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private readonly IRepository<RealCalendar, int> _realCalendars;
    private readonly List<Group> _groupSnapshot = [];
    private RealCalendar? _realCalendarSnapshot;

    public GroupsManagerJobTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<Rank, YearDayLeague>(builder => builder.WithInMemory());
        services.AddSharedBusiness();
        services.AddSingleton<ITeamCalculator>(_calculator);
        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _calendars = _services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _ranks = _services.GetRequiredService<IRepository<Rank, LeagueKey>>();
        _dailyRanks = _services.GetRequiredService<IRepository<Rank, YearDayLeague>>();
        _teams = _services.GetRequiredService<IRepository<Team, TeamDayKey>>();
        _realCalendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _groupSnapshot.AddRange(await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken));
        foreach (var group in _groupSnapshot)
            await _groups.DeleteAsync(group.Id, cancellationToken);
        _realCalendarSnapshot = await _realCalendars.GetAsync(FantacalcioTime.ActualYear, cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        await CleanupAsync(cancellationToken);
        _calculator.BuildRequest = null;
        _calculator.SettingsChanges = 0;
        _calculator.IsLiveResult = false;
        _calculator.OfficialResult = new VotedRealPlayerWrapper
        {
            Players =
            [
                new VotedRealPlayer { Name = "Roma player", RealTeam = new RealTeam { Name = "Roma" } },
                new VotedRealPlayer { Name = "Milan player", RealTeam = new RealTeam { Name = "Milan" } }
            ]
        };
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await CleanupAsync(cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        foreach (var group in _groupSnapshot)
            await _groups.InsertAsync(group.Id, group, cancellationToken);
        if (_realCalendarSnapshot != null)
            await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, _realCalendarSnapshot, cancellationToken);
    }

    [Fact]
    public async Task CalculatesResultsAndPersistsCurrentAndDailyRanks()
    {
        var year = FantacalcioTime.ActualYear;
        var settings = LeagueSetting.Default;
        settings.PointForFirstGoal = 70;
        settings.PointForNextGoal = 5;
        settings.PointInHome = 2;
        await _realCalendars.InsertAsync(year, RealCalendar(year), TestContext.Current.CancellationToken);
        await _groups.InsertAsync(GroupId, Group(year, settings), TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(year), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), Team(HomeOwner, "Home player"), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner), Team(AwayOwner, "Away player"), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal((year, 7), _calculator.BuildRequest);
        Assert.Equal(1, _calculator.SettingsChanges);
        var calendar = await _calendars.GetAsync(LeagueKey(), TestContext.Current.CancellationToken);
        Assert.NotNull(calendar);
        var result = Assert.Single(Assert.Single(calendar.Rounds["Regular"]).Games).Result;
        Assert.NotNull(result);
        Assert.Equal(74, result.Home.Value);
        Assert.Equal(66, result.Away.Value);
        Assert.Equal((1, 0), (result.HomeGoal, result.AwayGoal));

        var rank = await _ranks.GetAsync(LeagueKey(), TestContext.Current.CancellationToken);
        Assert.NotNull(rank);
        Assert.Equal(7, rank.SerieADay);
        var home = rank.RankedTeams["Regular"].Single(x => x.Owner == HomeOwner);
        var away = rank.RankedTeams["Regular"].Single(x => x.Owner == AwayOwner);
        Assert.Equal((3, 1, 1, 0), (home.Point, home.Victories, home.Goal, home.SufferedGoal));
        Assert.Equal((0, 1, 0, 1), (away.Point, away.Defeats, away.Goal, away.SufferedGoal));

        var dailyRank = await _dailyRanks.GetAsync(DailyRankKey(), TestContext.Current.CancellationToken);
        Assert.NotNull(dailyRank);
        Assert.Equal(7, dailyRank.SerieADay);
        Assert.Equal(3, dailyRank.RankedTeams["Regular"].Single(x => x.Owner == HomeOwner).Point);
    }

    [Fact]
    public async Task DoesNotCalculateResultsWithoutCompleteOfficialVotes()
    {
        var year = FantacalcioTime.ActualYear;
        await _realCalendars.InsertAsync(year, RealCalendar(year), TestContext.Current.CancellationToken);
        await _groups.InsertAsync(GroupId, Group(year, LeagueSetting.Default), TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(year), TestContext.Current.CancellationToken);
        _calculator.OfficialResult = null;

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var calendar = await _calendars.GetAsync(LeagueKey(), TestContext.Current.CancellationToken);
        Assert.Null(Assert.Single(Assert.Single(calendar!.Rounds["Regular"]).Games).Result);
        Assert.Null(await _ranks.GetAsync(LeagueKey(), TestContext.Current.CancellationToken));
    }

    private GroupsManagerJob CreateJob()
        => new(
            _services.GetRequiredService<IFantasoccerLogger>(),
            _calculator,
            _services,
            _services.GetRequiredService<IFactory<ILeagueCalculator>>(),
            _groups,
            _calendars,
            _dailyRanks,
            _ranks,
            _teams,
            _realCalendars);

    private static RealCalendar RealCalendar(int year)
        => new()
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    Year = year,
                    SerieADay = 7,
                    Games =
                    [
                        new RealGame
                        {
                            Home = new RealTeam { Name = "Roma" },
                            Away = new RealTeam { Name = "Milan" },
                            Date = FantacalcioTime.ItalyNow.AddHours(-12)
                        }
                    ]
                }
            ]
        };

    private static Group Group(int year, LeagueSetting settings)
        => new()
        {
            Id = GroupId,
            Name = "Groups manager test",
            Users = [],
            Leagues =
            [
                new League
                {
                    Id = LeagueId,
                    Name = "League",
                    Type = LeagueType.League,
                    BasketsId = [BasketId],
                    Years = [new AnnualLeague { Year = year, Settings = settings }]
                }
            ],
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Basket",
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

    private static Calendar LeagueCalendar(int year)
        => new()
        {
            Year = year,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["Regular"] =
                [
                    new Day
                    {
                        Number = 7,
                        SerieADay = 7,
                        Games =
                        [
                            new CalendarGame
                            {
                                Id = "groups-game",
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

    private static Team Team(string owner, string playerName)
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
                    Role = Role.Forward,
                    Position = FantaSoccerRole.Forward,
                    Status = PlayerInTeamStatus.Active
                }
            ]
        };

    private static LeagueKey LeagueKey()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear };

    private static YearDayLeague DailyRankKey()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear, Day = 7 };

    private static TeamDayKey TeamKey(string owner)
        => new()
        {
            Group = GroupId,
            BasketId = BasketId,
            Year = FantacalcioTime.ActualYear,
            Day = 7,
            Email = owner
        };

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _calendars.DeleteAsync(LeagueKey(), cancellationToken);
        await _ranks.DeleteAsync(LeagueKey(), cancellationToken);
        await _dailyRanks.DeleteAsync(DailyRankKey(), cancellationToken);
        await _teams.DeleteAsync(TeamKey(HomeOwner), cancellationToken);
        await _teams.DeleteAsync(TeamKey(AwayOwner), cancellationToken);
    }
}
