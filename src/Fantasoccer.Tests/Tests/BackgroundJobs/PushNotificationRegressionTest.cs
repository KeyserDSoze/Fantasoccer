using Fantasoccer.Business;
using Fantasoccer.Engine.GroupsManager;
using Fantasoccer.InMemory;
using Fantasoccer.Tests.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

// Regressions reproduced by the push audit, using only local repositories and a fake sender.
[Collection("InMemoryRepository")]
public sealed class PushNotificationRegressionTest : IAsyncLifetime
{
    private const string Id = "audit-push-local";
    private const string Home = "audit-home@test.local";
    private const string Away = "audit-away@test.local";
    private const string CoOwner = "audit-coowner@test.local";
    private readonly ServiceProvider _services;
    private readonly TestVoteRepositoryFactory _votes = new();
    private readonly AuditSender _sender = new();
    private readonly TestClock _clock = new();
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<LiveGroup, string> _liveGroups;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private readonly IRepository<RealCalendar, int> _realCalendars;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<PushNotificationSettings, string> _settings;
    private readonly IRepository<PushNotificationSent, YearDayKey> _sent;
    private List<Group> _oldGroups = [];
    private List<Entity<LiveGroup, string>> _oldLiveGroups = [];
    private RealCalendar? _oldCalendar;
    private static int Year => FantacalcioTime.ActualYear;
    private static YearDayKey Day => new() { Year = Year, Day = 7 };

    public PushNotificationRegressionTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<LiveGroup, string>(x => x.WithInMemory());
        services.AddRepository<PushNotificationSettings, string>(x => x.WithInMemory());
        services.AddRepository<PushNotificationSent, YearDayKey>(x => x.WithInMemory());
        services.AddSharedBusiness();
        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _liveGroups = _services.GetRequiredService<IRepository<LiveGroup, string>>();
        _teams = _services.GetRequiredService<IRepository<Team, TeamDayKey>>();
        _realCalendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
        _calendars = _services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _settings = _services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        _sent = _services.GetRequiredService<IRepository<PushNotificationSent, YearDayKey>>();
    }

    public async ValueTask InitializeAsync()
    {
        _oldGroups = await _groups.ToListAsEntityAsync();
        foreach (var group in _oldGroups) await _groups.DeleteAsync(group.Id);
        _oldLiveGroups = await _liveGroups.ToListAsync();
        foreach (var group in _oldLiveGroups) await _liveGroups.DeleteAsync(group.Key!);
        _oldCalendar = await _realCalendars.GetAsync(Year);
        await _realCalendars.DeleteAsync(Year);
        await _realCalendars.InsertAsync(Year, new RealCalendar
        {
            Year = Year,
            Days = [new RealDay { SerieADay = 7, Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddMinutes(-10) }] }]
        });
        _votes.Live.Set(Day, new VotedRealPlayerWrapper
        {
            Players = [new VotedRealPlayer { Name = "Mario Rossi", Role = Role.Forward, Vote = new Vote { Goal = 1, HasVote = true } }]
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _groups.DeleteAsync(Id);
        await _liveGroups.DeleteAsync(Id);
        await _realCalendars.DeleteAsync(Year);
        await new PushNotificationStateStore(_sent).SaveAsync(Day, new());
        await _sent.DeleteAsync(Day);
        foreach (var basket in new[] { "b1", "b2" })
        foreach (var owner in new[] { Home, Away }) await _teams.DeleteAsync(TeamKey(basket, owner));
        foreach (var league in new[] { "l1", "l2" }) await _calendars.DeleteAsync(LeagueKey(league));
        foreach (var email in new[] { Home, Away, CoOwner }) await _settings.DeleteAsync(email);
        foreach (var group in _oldGroups) await _groups.InsertAsync(group.Id, group);
        foreach (var group in _oldLiveGroups) await _liveGroups.InsertAsync(group.Key!, group.Value!);
        if (_oldCalendar != null) await _realCalendars.InsertAsync(Year, _oldCalendar);
    }

    private static TeamDayKey TeamKey(string basket, string owner) => new() { Group = Id, BasketId = basket, Email = owner, Year = Year, Day = 7 };
    private static LeagueKey LeagueKey(string league) => new() { Group = Id, League = league, Year = Year };
    private PushNotificationJob Job() => new(_services.GetRequiredService<IFantasoccerLogger>(), _services.GetRequiredService<ITeamCalculator>(), _groups, _liveGroups, _votes, _teams, _realCalendars, _sender, _calendars, _settings, _sent, timeProvider: _clock);
    private static Team Team(string owner, bool scorer, List<string>? coowners = null) => new()
    {
        Name = owner == Home ? "Home" : "Away", Owner = owner, AdditionalOwners = coowners ?? [],
        Players = [new Player { Name = scorer ? "Mario Rossi" : "Other player", Role = Role.Forward, Status = PlayerInTeamStatus.Active }]
    };

    private async Task SeedAsync(bool secondBasket = false, bool scorerInSecondBasket = false, List<string>? homeCoowners = null, List<string>? awayCoowners = null)
    {
        var basketIds = secondBasket ? new[] { "b1", "b2" } : ["b1"];
        var leagues = basketIds.Select((basket, i) => new League
        {
            Id = $"l{i + 1}", Name = $"League {i + 1}", BasketsId = [basket],
            Years = [new AnnualLeague { Year = Year, Settings = LeagueSetting.Default }]
        }).ToList();
        var group = new Group
        {
            Id = Id, Name = "Audit group", Users = [], Leagues = leagues,
            Baskets = basketIds.Select(basket => new Basket
            {
                Id = basket, Name = basket, Years = [new YearlyBasket { Year = Year, Teams = [
                    new AnnualTeam { Name = "Home", Owner = Home, AdditionalOwners = homeCoowners ?? [] },
                    new AnnualTeam { Name = "Away", Owner = Away, AdditionalOwners = awayCoowners ?? [] }
                ] }]
            }).ToList()
        };
        await _groups.InsertAsync(Id, group);
        await _liveGroups.InsertAsync(Id, new LiveGroup
        {
            Name = group.Name, Leagues = leagues.Select(l => new LiveLeague { Id = l.Id, Name = l.Name, Rounds = [], Rank = new Rank { RankedTeams = [] } }).ToList()
        });
        foreach (var league in leagues)
        {
            await _calendars.InsertAsync(LeagueKey(league.Id), new Calendar
            {
                Year = Year, Rounds = new() { ["regular"] = [new Day { Number = 7, SerieADay = 7, Games = [new CalendarGame { Id = "game", Home = "Home", Away = "Away", HomeOwner = Home, AwayOwner = Away }] }] }
            });
            var basket = league.BasketsId[0];
            await _teams.InsertAsync(TeamKey(basket, Home), Team(Home, basket == "b1" || scorerInSecondBasket, homeCoowners));
            await _teams.InsertAsync(TeamKey(basket, Away), Team(Away, false, awayCoowners));
        }
    }

    [Fact]
    public async Task OpponentEventShouldNotLeakIntoALeagueUsingAnotherBasket()
    {
        await SeedAsync(secondBasket: true);
        await _settings.InsertAsync(Away, new PushNotificationSettings { MatchEvents = false, OpponentMatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper!.WriteLine(string.Join("\n", _sender.Sent.Select(x => x.Message.Body)));
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task CoOwnerShouldReceiveOpponentEventsEvenWhenPrimaryOwnerHasNotOptedIn()
    {
        await SeedAsync(awayCoowners: [CoOwner]);
        await _settings.InsertAsync(CoOwner, new PushNotificationSettings { MatchEvents = false, OpponentMatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task OwnEventShouldReachCoOwnerFromSecondBasket()
    {
        await SeedAsync(secondBasket: true, scorerInSecondBasket: true);
        var key = TeamKey("b2", Home);
        var secondBasketTeam = (await _teams.GetAsync(key))!;
        secondBasketTeam.AdditionalOwners = [CoOwner];
        await _teams.UpdateAsync(key, secondBasketTeam);
        await _settings.InsertAsync(CoOwner, new PushNotificationSettings { MatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task SameRecipientShouldReceiveOwnEventOnlyOnce()
    {
        await SeedAsync(homeCoowners: [Home, CoOwner, CoOwner]);
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        await _settings.InsertAsync(CoOwner, new PushNotificationSettings { MatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task EventShouldStillBeAvailableAfterAnInterruptedSend()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        _sender.Fail = true;
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single((await _sent.GetAsync(Day))!.PendingNotifications);
        _sender.Fail = false;
        _clock.Advance(TimeSpan.FromMinutes(2));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent);
        Assert.Empty((await _sent.GetAsync(Day))!.PendingNotifications);
    }

    [Fact]
    public async Task SuccessfulRecipientsAreNotRetriedAfterAnotherRecipientFails()
    {
        await SeedAsync(homeCoowners: [CoOwner]);
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        await _settings.InsertAsync(CoOwner, new PushNotificationSettings { MatchEvents = true });
        _sender.FailEmail = CoOwner;
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent, s => s.Email == Home);
        _sender.FailEmail = null;
        _clock.Advance(TimeSpan.FromMinutes(2));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent, s => s.Email == Home);
        Assert.Single(_sender.Sent, s => s.Email == CoOwner);
    }

    [Fact]
    public async Task SuccessfulDevicesAreNotRetriedAfterAnotherDeviceFails()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true, Subscriptions = [
            new("https://push.test.local/one", "key", "auth"), new("https://push.test.local/two", "key", "auth")] });
        _sender.Partial = true;
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.Single(_sender.DeviceAttempts).Length);
        _sender.Partial = false;
        _clock.Advance(TimeSpan.FromMinutes(2));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Equal("https://push.test.local/two", Assert.Single(_sender.DeviceAttempts[1]));
        Assert.Equal(_sender.Sent[0].Message.Tag, _sender.Sent[1].Message.Tag);
    }

    [Fact]
    public async Task DisabledPreferencesCancelPendingDelivery()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        _sender.Fail = true;
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        await _settings.UpdateAsync(Home, new PushNotificationSettings { MatchEvents = false });
        _sender.Fail = false;
        _clock.Advance(TimeSpan.FromMinutes(2));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Empty(_sender.Sent);
        Assert.Empty((await _sent.GetAsync(Day))!.PendingNotifications);
    }

    [Fact]
    public async Task AllCalendarRoundsForTheSameSerieADayKeepTheirOwnMatchup()
    {
        await SeedAsync();
        var calendar = (await _calendars.GetAsync(LeagueKey("l1")))!;
        calendar.Rounds["playoffs"] = [new Day { Number = 1, SerieADay = 7, Games = [
            new CalendarGame { Id = "playoff-game", Home = "Home", Away = "Away", HomeOwner = Home, AwayOwner = Away }] }];
        await _calendars.UpdateAsync(LeagueKey("l1"), calendar);
        await _settings.InsertAsync(Away, new PushNotificationSettings { MatchEvents = false, OpponentMatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _sender.Sent.Count);
        Assert.NotEqual(_sender.Sent[0].Message.Tag, _sender.Sent[1].Message.Tag);
    }

    [Fact]
    public async Task RepeatedGoalAfterAnAnnulmentHasANewStableIdentity()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        foreach (var goals in new[] { 0, 1 })
        {
            _votes.Live.Set(Day, new VotedRealPlayerWrapper { Players = [new VotedRealPlayer
                { Name = "Mario Rossi", Role = Role.Forward, Vote = new Vote { Goal = goals, HasVote = true } }] });
            await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal(3, _sender.Sent.Count);
        Assert.Equal(3, _sender.Sent.Select(s => s.Message.Tag).Distinct().Count());
        Assert.Contains("annullato", _sender.Sent[1].Message.Body);
    }

    [Fact]
    public async Task ExpiredDeliveryIsNotSentLateAndIsRemovedFromTheActiveIndex()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        _sender.Fail = true;
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        _sender.Fail = false;
        _clock.Advance(TimeSpan.FromHours(49));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Empty(_sender.Sent);
        Assert.Empty((await _sent.GetAsync(Day))!.PendingNotifications);
        Assert.DoesNotContain(await new PushNotificationStateStore(_sent).GetPendingDaysAsync(), key => key.Year == Year && key.Day == 7);
    }

    [Fact]
    public async Task FailedQueueSaveDoesNotAdvanceTheCheckpointOrSendBeforeDurability()
    {
        await SeedAsync();
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = true });
        var failing = new FailingPushRepository<PushNotificationSent, YearDayKey>(_sent) { FailWrite = k => k.Day == 7 };
        var job = new PushNotificationJob(_services.GetRequiredService<IFantasoccerLogger>(), _services.GetRequiredService<ITeamCalculator>(),
            _groups, _liveGroups, _votes, _teams, _realCalendars, _sender, _calendars, _settings, failing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ActionToDoAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_sender.Sent);
        Assert.Null(await _sent.GetAsync(Day));
        Assert.Null(await _votes.Notification.GetAsync(Day));
        await Job().ActionToDoAsync(TestContext.Current.CancellationToken);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task FailedSettingsSaveDoesNotMutateTheCommittedDocument()
    {
        await _settings.InsertAsync(Home, new PushNotificationSettings { MatchEvents = false });
        var service = new PushNotificationSettingsService(new FailingPushRepository<PushNotificationSettings, string>(_settings), new InMemoryPushNotificationLock());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(Home, new PushNotificationPreferences { MatchEvents = true }));
        Assert.False((await _settings.GetAsync(Home))!.MatchEvents);
    }

    [Fact]
    public async Task PersistedOutboxIsDetachedFromObjectsStillBeingMutated()
    {
        var store = new PushNotificationStateStore(_sent);
        var record = new PushNotificationSent { PendingNotifications = [new PendingPushNotification { Id = "test" }] };
        await store.SaveAsync(Day, record);
        record.PendingNotifications.Clear();
        Assert.Single((await store.GetAsync(Day)).PendingNotifications);
        Assert.Contains(await store.GetPendingDaysAsync(), key => key.Year == Year && key.Day == 7);
        await store.SaveAsync(Day, record);
        Assert.DoesNotContain(await store.GetPendingDaysAsync(), key => key.Year == Year && key.Day == 7);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class AuditSender : IPushNotificationService
    {
        public bool Fail;
        public string? FailEmail;
        public bool Partial;
        public List<string[]> DeviceAttempts { get; } = [];
        public List<(string Email, Notification Message)> Sent { get; } = [];
        public ValueTask<PushNotificationDeliveryResult> SendNotificationAsync(string email, PushNotificationSettings settings, Notification message, CancellationToken cancellationToken = default)
        {
            if (Fail || email == FailEmail) throw new InvalidOperationException("Simulated interrupted send");
            var endpoints = settings.Subscriptions.Select(s => s.Endpoint).ToArray();
            lock (Sent)
            {
                Sent.Add((email, message));
                DeviceAttempts.Add(endpoints);
            }
            return ValueTask.FromResult(new PushNotificationDeliveryResult
            {
                CompletedEndpoints = Partial ? endpoints.Take(1).ToList() : endpoints.ToList(),
                RetryEndpoints = Partial ? endpoints.Skip(1).ToList() : []
            });
        }
        public PushNotificationStatistics GetStatistics() => new();
        public void ResetStatistics() => Sent.Clear();
    }
}
