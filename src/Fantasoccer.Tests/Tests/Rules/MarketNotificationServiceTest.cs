using Fantasoccer.Business;
using Fantasoccer.InMemory;
using Fantasoccer.Engine.GroupsManager;
using Fantasoccer.Tests.Tests.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.Rules;

[Collection("InMemoryRepository")]
public sealed class MarketNotificationServiceTest : IAsyncLifetime
{
    private const string GroupId = "market-notification-test";
    private const string LeagueId = "market-notification-league";
    private const string BasketId = "market-notification-basket";
    private const string Buyer = "market-buyer@test.local";
    private const string Seller = "market-seller@test.local";
    private const string Voter = "market-voter@test.local";
    private const string SecondaryLeagueId = "market-notification-secondary-league";
    private readonly ServiceProvider _services;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<PushNotificationSettings, string> _settings;
    private readonly TestPushNotificationService _notifications = new();
    private readonly IMarketNotificationService _service;

    public MarketNotificationServiceTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<PushNotificationSettings, string>(builder => builder.WithInMemory());
        services.AddRepository<PushNotificationSent, YearDayKey>(builder => builder.WithInMemory());
        services.AddRepository<LiveGroup, string>(builder => builder.WithInMemory());
        services.AddSingleton<IPushNotificationService>(_notifications);
        services.AddSharedBusiness();
        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _settings = _services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        _service = _services.GetRequiredService<IMarketNotificationService>();
    }

    public async ValueTask InitializeAsync()
        => await CleanupAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProposalCreatedNotifiesOnlyTheSeller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Buyer, new PushNotificationSettings { MarketEvents = true }, cancellationToken);

        await _service.NotifyProposalCreatedAsync(Key(), Market(MarketStatus.Pending), cancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(Seller, sent.Email);
        Assert.Equal("[Market league] Buyer Team ti ha proposto uno scambio.", sent.Notification.Body);
        Assert.StartsWith("https://fanta.zone/market/trades?", sent.Notification.Data.Uri);
    }

    [Fact]
    public async Task ApprovedOutcomeNotifiesMainLeagueMembersExceptTheActor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await InsertMarketSettingsAsync(cancellationToken);

        await _service.NotifyMarketChangedAsync(Key(), Market(MarketStatus.Approved), MarketNotificationEvent.VoteCast, Seller, cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.Contains(_notifications.Sent, x => x.Email == Buyer);
        Assert.Contains(_notifications.Sent, x => x.Email == Voter);
        Assert.DoesNotContain(_notifications.Sent, x => x.Email == Seller);
        Assert.All(_notifications.Sent, x => Assert.Equal("[Market league] Lo scambio tra Buyer Team e Seller Team è stato completato.", x.Notification.Body));
    }

    [Fact]
    public async Task DeniedOutcomeNotifiesMainLeagueMembersExceptTheActor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await InsertMarketSettingsAsync(cancellationToken);

        await _service.NotifyMarketChangedAsync(Key(), Market(MarketStatus.Denied), MarketNotificationEvent.SellerRejected, Seller, cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.All(_notifications.Sent, x => Assert.Contains("è stata rifiutata", x.Notification.Body));
    }

    [Fact]
    public async Task SellerAcceptanceNotifiesTheLeagueThatVotingIsOpen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await InsertMarketSettingsAsync(cancellationToken);
        var market = Market(MarketStatus.Pending);
        market.SellerAccepted = true;
        market.Approvers = [Buyer, Seller];

        await _service.NotifyMarketChangedAsync(Key(), market, MarketNotificationEvent.SellerAccepted, Seller, cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.Contains(_notifications.Sent, x => x.Email == Buyer);
        Assert.Contains(_notifications.Sent, x => x.Email == Voter);
        Assert.All(_notifications.Sent, x => Assert.Contains("Votazione aperta: 2 sì, 0 no", x.Notification.Body));
    }

    [Fact]
    public async Task NewPendingVoteNotifiesTheLeagueWithUpdatedVoteCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await InsertMarketSettingsAsync(cancellationToken);
        var market = Market(MarketStatus.Pending);
        market.SellerAccepted = true;
        market.Approvers = [Buyer, Seller, Voter];

        await _service.NotifyMarketChangedAsync(Key(), market, MarketNotificationEvent.VoteCast, Voter, cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.DoesNotContain(_notifications.Sent, x => x.Email == Voter);
        Assert.All(_notifications.Sent, x => Assert.Contains("Voter Team ha votato a favore lo scambio (3 sì, 0 no)", x.Notification.Body));
    }

    [Fact]
    public async Task FailedMarketSendIsRetriedEvenWithoutALiveCalendar()
    {
        var ct = TestContext.Current.CancellationToken;
        await InsertGroupAsync(ct);
        await InsertMarketSettingsAsync(ct);
        _notifications.Fail = true;
        await _service.NotifyProposalCreatedAsync(Key(), Market(MarketStatus.Pending), ct);
        var state = new PushNotificationStateStore(_services.GetRequiredService<IRepository<PushNotificationSent, YearDayKey>>());
        var key = new YearDayKey { Year = FantacalcioTime.ActualYear, Day = 0 };
        var record = await state.GetAsync(key, ct);
        var pending = Assert.Single(record.PendingNotifications.Where(p => p.Email == Seller));
        var originalTag = pending.Notification.Tag;
        pending.NextAttemptAt = DateTimeOffset.MinValue;
        await state.SaveAsync(key, record, ct);
        _notifications.Fail = false;
        var calendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
        var previous = await calendars.GetAsync(key.Year, ct);
        await calendars.DeleteAsync(key.Year, ct);
        try
        {
            var job = new PushNotificationJob(_services.GetRequiredService<IFantasoccerLogger>(), _services.GetRequiredService<ITeamCalculator>(),
                _groups, _services.GetRequiredService<IRepository<LiveGroup, string>>(),
                _services.GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>(),
                _services.GetRequiredService<IRepository<Team, TeamDayKey>>(), calendars, _notifications,
                _services.GetRequiredService<IRepository<Calendar, LeagueKey>>(), _settings,
                _services.GetRequiredService<IRepository<PushNotificationSent, YearDayKey>>());
            await job.ActionToDoAsync(ct);
            Assert.Equal(originalTag, Assert.Single(_notifications.Sent).Notification.Tag);
            Assert.DoesNotContain((await state.GetAsync(key, ct)).PendingNotifications, p => p.Email == Seller);
        }
        finally { if (previous != null) await calendars.InsertAsync(key.Year, previous, ct); }
    }

    [Fact]
    public async Task SecondaryLeagueDoesNotSendMarketNotifications()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await InsertMarketSettingsAsync(cancellationToken);

        await _service.NotifyProposalCreatedAsync(SecondaryKey(), Market(MarketStatus.Pending), cancellationToken);

        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task DoesNotNotifyUsersWithMarketEventsDisabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = false }, cancellationToken);

        await _service.NotifyProposalCreatedAsync(Key(), Market(MarketStatus.Pending), cancellationToken);

        Assert.Empty(_notifications.Sent);
    }

    private async Task InsertGroupAsync(CancellationToken cancellationToken)
    {
        var settings = LeagueSetting.Default;
        settings.Market = MarketType.WithVote;
        await _groups.InsertAsync(GroupId, new Group
        {
            Id = GroupId,
            Name = "Market group",
            Users = [],
            Leagues =
            [
                new League
                {
                    Id = LeagueId,
                    Name = "Market league",
                    IsMain = true,
                    Type = LeagueType.League,
                    BasketsId = [BasketId],
                    Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = settings }]
                },
                new League
                {
                    Id = SecondaryLeagueId,
                    Name = "Secondary league",
                    IsMain = false,
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
                                new AnnualTeam { Name = "Buyer Team", Owner = Buyer, AdditionalOwners = [] },
                                new AnnualTeam { Name = "Seller Team", Owner = Seller, AdditionalOwners = [] },
                                new AnnualTeam { Name = "Voter Team", Owner = Voter, AdditionalOwners = [] }
                            ]
                        }
                    ]
                }
            ]
        }, cancellationToken);
    }

    private static Market Market(MarketStatus status)
        => new()
        {
            Id = "market-1",
            Buyer = Buyer,
            Seller = Seller,
            Status = status,
            Approvers = [],
            Deniers = [],
            BuyerPlayers = [],
            SellerPlayers = []
        };

    private static LeagueKey Key()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear };

    private static LeagueKey SecondaryKey()
        => new() { Group = GroupId, League = SecondaryLeagueId, Year = FantacalcioTime.ActualYear };

    private async Task InsertMarketSettingsAsync(CancellationToken cancellationToken)
    {
        await _settings.InsertAsync(Buyer, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Voter, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _settings.DeleteAsync(Buyer, cancellationToken);
        await _settings.DeleteAsync(Seller, cancellationToken);
        await _settings.DeleteAsync(Voter, cancellationToken);
        _notifications.Sent.Clear();
        _notifications.Fail = false;
        var state = new PushNotificationStateStore(_services.GetRequiredService<IRepository<PushNotificationSent, YearDayKey>>());
        var key = new YearDayKey { Year = FantacalcioTime.ActualYear, Day = 0 };
        var record = await state.GetAsync(key, cancellationToken);
        record.PendingNotifications.RemoveAll(p => new[] { Buyer, Seller, Voter }.Contains(p.Email));
        await state.SaveAsync(key, record, cancellationToken);
    }
}
