using Fantasoccer.Business;
using Fantasoccer.InMemory;
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
        _services.Dispose();
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
        Assert.Equal("https://fanta.zone/market", sent.Notification.Data.Uri);
    }

    [Fact]
    public async Task ApprovedOutcomeNotifiesBuyerAndSeller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Buyer, new PushNotificationSettings { MarketEvents = true }, cancellationToken);

        await _service.NotifyOutcomeAsync(Key(), Market(MarketStatus.Approved), cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.Contains(_notifications.Sent, x => x.Email == Buyer);
        Assert.Contains(_notifications.Sent, x => x.Email == Seller);
        Assert.All(_notifications.Sent, x => Assert.Equal("[Market league] Scambio tra Buyer Team e Seller Team completato!", x.Notification.Body));
    }

    [Fact]
    public async Task DeniedOutcomeNotifiesBuyerAndSeller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Buyer, new PushNotificationSettings { MarketEvents = true }, cancellationToken);

        await _service.NotifyOutcomeAsync(Key(), Market(MarketStatus.Denied), cancellationToken);

        Assert.Equal(2, _notifications.Sent.Count);
        Assert.All(_notifications.Sent, x => Assert.Contains("è stata rifiutata", x.Notification.Body));
    }

    [Fact]
    public async Task PendingOutcomeDoesNotNotify()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertGroupAsync(cancellationToken);
        await _settings.InsertAsync(Seller, new PushNotificationSettings { MarketEvents = true }, cancellationToken);
        await _settings.InsertAsync(Buyer, new PushNotificationSettings { MarketEvents = true }, cancellationToken);

        await _service.NotifyOutcomeAsync(Key(), Market(MarketStatus.Pending), cancellationToken);

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
                                new AnnualTeam { Name = "Seller Team", Owner = Seller, AdditionalOwners = [] }
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

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _settings.DeleteAsync(Buyer, cancellationToken);
        await _settings.DeleteAsync(Seller, cancellationToken);
        _notifications.Sent.Clear();
    }
}
