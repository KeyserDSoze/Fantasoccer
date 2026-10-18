using Fantasoccer.Business;
using Fantasoccer.Engine.GroupsManager;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;
using Rystem;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class PushNotificationJobTest : IAsyncLifetime
{
    private const string GroupId = "push-notification-job-test";
    private const string LeagueId = "push-notification-league-test";
    private const string SecondLeagueId = "push-notification-second-league-test";
    private const string BestLeagueId = "push-notification-best-league-test";
    private const string BasketId = "push-notification-basket-test";
    private const string HomeOwner = "push-home@test.local";
    private const string AwayOwner = "push-away@test.local";
    private readonly ServiceProvider _services;
    private readonly TestPushNotificationService _notifications = new();
    private readonly TestVoteRepositoryFactory _voteFactory = new();
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<LiveGroup, string> _liveGroups;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private readonly IRepository<RealCalendar, int> _realCalendars;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<PushNotificationSettings, string> _settings;
    private readonly IRepository<PushNotificationSent, YearDayKey> _sent;
    private readonly TestVoteRepository _liveVotes;
    private readonly TestVoteRepository _notificationVotes;
    private readonly List<Group> _groupSnapshot = [];
    private RealCalendar? _realCalendarSnapshot;

    public PushNotificationJobTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<LiveGroup, string>(builder => builder.WithInMemory());
        services.AddRepository<PushNotificationSettings, string>(builder => builder.WithInMemory());
        services.AddRepository<PushNotificationSent, YearDayKey>(builder => builder.WithInMemory());
        services.AddSharedBusiness();
        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _liveGroups = _services.GetRequiredService<IRepository<LiveGroup, string>>();
        _teams = _services.GetRequiredService<IRepository<Team, TeamDayKey>>();
        _realCalendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
        _calendars = _services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _settings = _services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        _sent = _services.GetRequiredService<IRepository<PushNotificationSent, YearDayKey>>();
        _liveVotes = _voteFactory.Live;
        _notificationVotes = _voteFactory.Notification;
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
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        await CleanupAsync(cancellationToken);
        foreach (var group in _groupSnapshot)
            await _groups.InsertAsync(group.Id, group, cancellationToken);
        if (_realCalendarSnapshot != null)
            await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, _realCalendarSnapshot, cancellationToken);
    }

    [Fact]
    public async Task SendsMatchEventToOwnerAndOpponentOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, RealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(), cancellationToken);
        await _liveGroups.InsertAsync(GroupId, LiveGroup(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(), cancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), Team(HomeOwner, "Home", true), cancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner), Team(AwayOwner, "Away", false), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { MatchEvents = true }, cancellationToken);
        await _settings.InsertAsync(AwayOwner, new PushNotificationSettings { OpponentMatchEvents = true }, cancellationToken);
        _liveVotes.Set(DayKey(), new VotedRealPlayerWrapper
        {
            Players =
            [
                new VotedRealPlayer
                {
                    Name = "Mario Rossi",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    Vote = new Vote { Goal = 1, HasVote = true }
                }
            ]
        });
        var job = CreateJob();
        Assert.Null(await _notificationVotes.GetAsync(DayKey(), cancellationToken));

        await job.ActionToDoAsync(cancellationToken);
        await job.ActionToDoAsync(cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        var direct = _notifications.Sent.Single(x => x.Email == HomeOwner);
        Assert.Equal("[Push group] Mario Rossi: Goal", direct.Notification.Body);
        Assert.Equal("https://fanta.zone/live", direct.Notification.Data.Uri);
        var opponent = _notifications.Sent.Single(x => x.Email == AwayOwner);
        Assert.Equal("[Push group - Test league] Mario Rossi: Goal (Home)", opponent.Notification.Body);
        Assert.NotEqual(direct.Notification.Tag, opponent.Notification.Tag);
        var baseline = await _notificationVotes.GetAsync(DayKey(), cancellationToken);
        Assert.Equal(1, Assert.Single(baseline!.Players).Vote.Goal);
    }

    [Fact]
    public async Task SendsOwnYellowCardWhenOpponentNotificationsAreDisabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, RealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(), cancellationToken);
        await _liveGroups.InsertAsync(GroupId, LiveGroup(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(), cancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), Team(HomeOwner, "Home", true), cancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner), Team(AwayOwner, "Away", false), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { MatchEvents = true }, cancellationToken);
        _liveVotes.Set(DayKey(), new VotedRealPlayerWrapper
        {
            Players =
            [
                new VotedRealPlayer
                {
                    Name = "Mario Rossi",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    Vote = new Vote { Status = Behaviour.YellowCard, HasVote = true }
                }
            ]
        });

        await CreateJob().ActionToDoAsync(cancellationToken);

        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(HomeOwner, notification.Email);
        Assert.Equal("[Push group] Mario Rossi: Giallo", notification.Notification.Body);
    }

    [Fact]
    public async Task DoesNotNotifyForAnEventFromAPlayerWhoIsNoLongerActive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, RealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(), cancellationToken);
        await _liveGroups.InsertAsync(GroupId, LiveGroup(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(), cancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), Team(HomeOwner, "Home", true, PlayerInTeamStatus.Sold), cancellationToken);
        await _teams.InsertAsync(TeamKey(AwayOwner), Team(AwayOwner, "Away", false), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { MatchEvents = true }, cancellationToken);
        await _settings.InsertAsync(AwayOwner, new PushNotificationSettings { OpponentMatchEvents = true }, cancellationToken);
        _liveVotes.Set(DayKey(), new VotedRealPlayerWrapper
        {
            Players =
            [
                new VotedRealPlayer
                {
                    Name = "Mario Rossi",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    Vote = new Vote { Goal = 1, HasVote = true }
                }
            ]
        });

        await CreateJob().ActionToDoAsync(cancellationToken);

        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task SendsDeploymentReminderOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 7,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-12), Delayed = false }]
                },
                new RealDay
                {
                    SerieADay = 8,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(4), Delayed = false }]
                }
            ]
        }, cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { DeploymentReminder = true }, cancellationToken);
        var job = CreateJob();

        await job.ActionToDoAsync(cancellationToken);
        await job.ActionToDoAsync(cancellationToken);

        var reminder = Assert.Single(_notifications.Sent);
        Assert.Equal(HomeOwner, reminder.Email);
        Assert.Equal("Ricorda di schierare la formazione!", reminder.Notification.Body);
        Assert.Equal("https://fanta.zone/edit", reminder.Notification.Data.Uri);
        Assert.NotNull(await _sent.GetAsync(ReminderKey(), cancellationToken));
    }

    [Fact]
    public async Task SendsEndDayResultWithPersonalOutcomeOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, EndedRealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), EndedLeagueCalendar(), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { EndDay = true }, cancellationToken);
        await _settings.InsertAsync(AwayOwner, new PushNotificationSettings { EndDay = true }, cancellationToken);
        var job = CreateJob();

        await job.ActionToDoAsync(cancellationToken);
        await job.ActionToDoAsync(cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        var home = _notifications.Sent.Single(x => x.Email == HomeOwner);
        Assert.Equal("[Test league] Giornata 7: Hai vinto contro Away 2-1 (78.5 pt)", home.Notification.Body);
        Assert.Equal("https://fanta.zone/calendar", home.Notification.Data.Uri);
        var away = _notifications.Sent.Single(x => x.Email == AwayOwner);
        Assert.Equal("[Test league] Giornata 7: Hai perso contro Home 1-2 (66 pt)", away.Notification.Body);
        var sent = await _sent.GetAsync(DayKey(), cancellationToken);
        Assert.True(sent!.EndDay);
    }

    [Fact]
    public async Task DoesNotSendEndDayUntilResultsAreAvailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, EndedRealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { EndDay = true }, cancellationToken);
        var job = CreateJob();

        await job.ActionToDoAsync(cancellationToken);

        Assert.Empty(_notifications.Sent);
        var sent = await _sent.GetAsync(DayKey(), cancellationToken);
        Assert.True(sent == null || !sent.EndDay);
    }

    [Fact]
    public async Task SendsLiveChangesReminderToTeamsWithRemainingChangesOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, EndedRealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(liveFormationChanges: 2), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { LiveChangesReminder = true }, cancellationToken);
        var job = CreateJob();

        await job.ActionToDoAsync(cancellationToken);
        await job.ActionToDoAsync(cancellationToken);

        var reminder = Assert.Single(_notifications.Sent);
        Assert.Equal(HomeOwner, reminder.Email);
        Assert.StartsWith("[Test league] Ti restano 2 cambi formazione: usali entro le ", reminder.Notification.Body);
        Assert.Equal("https://fanta.zone/live", reminder.Notification.Data.Uri);
        var sent = await _sent.GetAsync(DayKey(), cancellationToken);
        Assert.True(sent!.LiveChangesReminder);
    }

    [Fact]
    public async Task DoesNotSendLiveChangesReminderWhenNoRemainingChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, EndedRealCalendar(), cancellationToken);
        await _groups.InsertAsync(GroupId, Group(liveFormationChanges: 2), cancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), TeamWithChanges(HomeOwner, "Home", 2), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { LiveChangesReminder = true }, cancellationToken);
        var job = CreateJob();

        await job.ActionToDoAsync(cancellationToken);

        Assert.DoesNotContain(_notifications.Sent, x => x.Notification.Body.Contains("cambi formazione") || x.Notification.Body.Contains("cambio formazione"));
    }

    [Fact]
    public async Task SendsLiveFormationChangeWithScoreImpactForManualFormationLeaguesOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, RealCalendar(), cancellationToken);
        var group = Group();
        var secondSettings = LeagueSetting.Default;
        var bestSettings = LeagueSetting.Default;
        bestSettings.Formation = FormationType.Best;
        group.Leagues.Add(new League
        {
            Id = SecondLeagueId,
            Name = "Second league",
            Type = LeagueType.Cup,
            BasketsId = [BasketId],
            Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = secondSettings }]
        });
        group.Leagues.Add(new League
        {
            Id = BestLeagueId,
            Name = "Best League",
            Type = LeagueType.SuperLeague,
            BasketsId = [BasketId],
            Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = bestSettings }]
        });
        await _groups.InsertAsync(GroupId, group, cancellationToken);
        await _calendars.InsertAsync(LeagueKey(), LeagueCalendar(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(SecondLeagueId), LeagueCalendar(), cancellationToken);
        await _calendars.InsertAsync(LeagueKey(BestLeagueId), LeagueCalendar(), cancellationToken);
        await _teams.InsertAsync(TeamKey(HomeOwner), TeamWithLiveFormationChange(), cancellationToken);
        await _settings.InsertAsync(HomeOwner, new PushNotificationSettings { LiveFormationChangeEvents = true }, cancellationToken);
        var calculatorLiveVotes = _services
            .GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>()
            .Create("live")!;
        await calculatorLiveVotes.InsertAsync(DayKey(), new VotedRealPlayerWrapper
        {
            Players =
            [
                new VotedRealPlayer
                {
                    Name = "Mario Rossi",
                    Role = Role.Forward,
                    Vote = new Vote { Value = 7, HasVote = true }
                },
                new VotedRealPlayer
                {
                    Name = "Luigi Bianchi",
                    Role = Role.Forward,
                    Vote = new Vote { Value = 5, HasVote = true }
                }
            ]
        }, cancellationToken);

        var job = CreateJob();
        await job.ActionToDoAsync(cancellationToken);
        await job.ActionToDoAsync(cancellationToken);

        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(HomeOwner, notification.Email);
        Assert.Contains("Mario Rossi (ATT → 1ª riserva ATT)", notification.Notification.Body);
        Assert.Contains("Test league: 9 → 7 pt (-2)", notification.Notification.Body);
        Assert.Contains("Second league: 9 → 7 pt (-2)", notification.Notification.Body);
        Assert.DoesNotContain("Best League", notification.Notification.Body);
        var sent = await _sent.GetAsync(DayKey(), cancellationToken);
        Assert.Single(sent!.LiveFormationChangeNotifications);
    }

    private PushNotificationJob CreateJob()
        => new(
            _services.GetRequiredService<IFantasoccerLogger>(),
            _services.GetRequiredService<ITeamCalculator>(),
            _groups,
            _liveGroups,
            _voteFactory,
            _teams,
            _realCalendars,
            _notifications,
            _calendars,
            _settings,
            _sent);

    private static RealCalendar RealCalendar()
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 7,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddMinutes(-10), Delayed = false }]
                }
            ]
        };

    private static RealCalendar EndedRealCalendar()
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 7,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-3), Delayed = false }]
                }
            ]
        };

    private static Group Group(int liveFormationChanges = 0)
    {
        var settings = LeagueSetting.Default;
        settings.LiveFormationChanges = liveFormationChanges;
        return new()
        {
            Id = GroupId,
            Name = "Push group",
            Users = [],
            Leagues =
            [
                new League
                {
                    Id = LeagueId,
                    Name = "Test league",
                    Type = LeagueType.League,
                    BasketsId = [BasketId],
                    Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = settings }]
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
                            Year = FantacalcioTime.ActualYear,
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
    }

    private static LiveGroup LiveGroup()
        => new()
        {
            Name = "Push group",
            Leagues =
            [
                new LiveLeague
                {
                    Id = LeagueId,
                    Name = "Test league",
                    Rounds = [],
                    Rank = new Rank { RankedTeams = [] }
                }
            ]
        };

    private static Calendar LeagueCalendar()
        => new()
        {
            Year = FantacalcioTime.ActualYear,
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
                                Id = "push-game",
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

    private static Team Team(
        string owner,
        string name,
        bool hasScorer,
        PlayerInTeamStatus scorerStatus = PlayerInTeamStatus.Active)
        => new()
        {
            Name = name,
            Owner = owner,
            AdditionalOwners = [],
            Players = hasScorer
                ? [new Player { Name = "Mario Rossi", Role = Role.Forward, Status = scorerStatus }]
                : [new Player { Name = "Other player", Role = Role.Forward, Status = PlayerInTeamStatus.Active }]
        };

    private static Team TeamWithChanges(string owner, string name, int formationChanges)
        => new()
        {
            Name = name,
            Owner = owner,
            AdditionalOwners = [],
            FormationChanges = formationChanges,
            Players = [new Player { Name = "Mario Rossi", Role = Role.Forward, Status = PlayerInTeamStatus.Active }]
        };

    private static Team TeamWithLiveFormationChange()
        => new()
        {
            Name = "Home",
            Owner = HomeOwner,
            AdditionalOwners = [],
            FormationChanges = 1,
            FormationChangeHistory =
            [
                new LiveFormationChange
                {
                    Date = FantacalcioTime.ItalyNow,
                    Author = "Coach",
                    Players =
                    [
                        new LiveFormationPlayerChange
                        {
                            Name = "Mario Rossi",
                            FromPosition = FantaSoccerRole.Forward,
                            ToPosition = FantaSoccerRole.FirstBackupForward
                        },
                        new LiveFormationPlayerChange
                        {
                            Name = "Luigi Bianchi",
                            FromPosition = FantaSoccerRole.FirstBackupForward,
                            ToPosition = FantaSoccerRole.Forward
                        }
                    ]
                }
            ],
            Players =
            [
                new Player
                {
                    Name = "Mario Rossi",
                    Role = Role.Forward,
                    Position = FantaSoccerRole.FirstBackupForward,
                    Status = PlayerInTeamStatus.Active
                },
                new Player
                {
                    Name = "Luigi Bianchi",
                    Role = Role.Forward,
                    Position = FantaSoccerRole.Forward,
                    Status = PlayerInTeamStatus.Active
                }
            ]
        };

    private static Calendar EndedLeagueCalendar()
        => new()
        {
            Year = FantacalcioTime.ActualYear,
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
                                Id = "push-game",
                                Home = "Home",
                                HomeOwner = HomeOwner,
                                Away = "Away",
                                AwayOwner = AwayOwner,
                                Result = new GameResult
                                {
                                    Home = new Point(78.5f, false, false, false),
                                    Away = new Point(66f, false, false, false),
                                    HomeGoal = 2,
                                    AwayGoal = 1
                                }
                            }
                        ]
                    }
                ]
            }
        };

    private static LeagueKey LeagueKey(string? leagueId = null)
        => new() { Group = GroupId, League = leagueId ?? LeagueId, Year = FantacalcioTime.ActualYear };

    private static YearDayKey DayKey()
        => new() { Year = FantacalcioTime.ActualYear, Day = 7 };

    private static YearDayKey ReminderKey()
        => new() { Year = FantacalcioTime.ActualYear, Day = 8 };

    private static TeamDayKey TeamKey(string owner)
        => new()
        {
            Group = GroupId,
            BasketId = BasketId,
            Email = owner,
            Year = FantacalcioTime.ActualYear,
            Day = 7
        };

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _liveGroups.DeleteAsync(GroupId, cancellationToken);
        await _calendars.DeleteAsync(LeagueKey(), cancellationToken);
        await _calendars.DeleteAsync(LeagueKey(SecondLeagueId), cancellationToken);
        await _calendars.DeleteAsync(LeagueKey(BestLeagueId), cancellationToken);
        await _teams.DeleteAsync(TeamKey(HomeOwner), cancellationToken);
        await _teams.DeleteAsync(TeamKey(AwayOwner), cancellationToken);
        await _settings.DeleteAsync(HomeOwner, cancellationToken);
        await _settings.DeleteAsync(AwayOwner, cancellationToken);
        await _sent.DeleteAsync(DayKey(), cancellationToken);
        await _sent.DeleteAsync(ReminderKey(), cancellationToken);
        _liveVotes.Clear();
        _notificationVotes.Clear();
        await _services
            .GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>()
            .Create("live")!
            .DeleteAsync(DayKey(), cancellationToken);
        _notifications.Sent.Clear();
    }
}

public sealed class TestPushNotificationService : IPushNotificationService
{
    public List<(string Email, Notification Notification)> Sent { get; } = [];

    public ValueTask SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message)
    {
        Sent.Add((email, message));
        return ValueTask.CompletedTask;
    }

    public PushNotificationStatistics GetStatistics()
        => new() { TotalAttempted = Sent.Count, TotalSucceeded = Sent.Count };

    public void ResetStatistics()
        => Sent.Clear();
}

public sealed class TestVoteRepositoryFactory : IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>
{
    public TestVoteRepository Live { get; } = new();
    public TestVoteRepository Notification { get; } = new();

    public IRepository<VotedRealPlayerWrapper, YearDayKey>? Create(AnyOf<string?, Enum>? name = null)
        => name?.ToString() == "notification" ? Notification : Live;

    public IRepository<VotedRealPlayerWrapper, YearDayKey>? CreateWithoutDecoration(AnyOf<string?, Enum>? name = null)
        => Create(name);

    public IEnumerable<IRepository<VotedRealPlayerWrapper, YearDayKey>> CreateAll(AnyOf<string?, Enum>? name = null)
        => [Live, Notification];

    public IEnumerable<IRepository<VotedRealPlayerWrapper, YearDayKey>> CreateAllWithoutDecoration(AnyOf<string?, Enum>? name = null)
        => CreateAll(name);

    public bool Exists(AnyOf<string?, Enum>? name = null)
        => name?.ToString() is "live" or "notification";
}

public sealed class TestVoteRepository : IRepository<VotedRealPlayerWrapper, YearDayKey>
{
    private readonly Dictionary<string, VotedRealPlayerWrapper> _values = [];

    public void Set(YearDayKey key, VotedRealPlayerWrapper value)
        => _values[Key(key)] = value;

    public void Clear()
        => _values.Clear();

    public Task<State<VotedRealPlayerWrapper, YearDayKey>> InsertAsync(
        YearDayKey key,
        VotedRealPlayerWrapper value,
        CancellationToken cancellationToken = default)
    {
        Set(key, value);
        return Task.FromResult<State<VotedRealPlayerWrapper, YearDayKey>>(null!);
    }

    public Task<State<VotedRealPlayerWrapper, YearDayKey>> UpdateAsync(
        YearDayKey key,
        VotedRealPlayerWrapper value,
        CancellationToken cancellationToken = default)
    {
        Set(key, value);
        return Task.FromResult<State<VotedRealPlayerWrapper, YearDayKey>>(null!);
    }

    public Task<State<VotedRealPlayerWrapper, YearDayKey>> DeleteAsync(
        YearDayKey key,
        CancellationToken cancellationToken = default)
    {
        _values.Remove(Key(key));
        return Task.FromResult<State<VotedRealPlayerWrapper, YearDayKey>>(null!);
    }

    public Task<State<VotedRealPlayerWrapper, YearDayKey>> ExistAsync(
        YearDayKey key,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<VotedRealPlayerWrapper?> GetAsync(
        YearDayKey key,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(Key(key)));

    public IAsyncEnumerable<Entity<VotedRealPlayerWrapper, YearDayKey>> QueryAsync(
        IFilterExpression filter,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public ValueTask<TProperty> OperationAsync<TProperty>(
        OperationType<TProperty> operation,
        IFilterExpression filter,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<BatchResult<VotedRealPlayerWrapper, YearDayKey>> BatchAsync(
        BatchOperations<VotedRealPlayerWrapper, YearDayKey> operations,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public ValueTask<bool> BootstrapAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(true);

    private static string Key(YearDayKey key)
        => $"{key.Year}:{key.Day}";
}
