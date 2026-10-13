using Fantasoccer.Business;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Rules;

[Collection("InMemoryRepository")]
public sealed class MarketManagerRulesTest : IAsyncLifetime
{
    private const string GroupId = "market-manager-test";
    private const string LeagueId = "market-manager-league";
    private const string BasketId = "market-manager-basket";
    private const string Buyer = "buyer@test.local";
    private const string Seller = "seller@test.local";
    private readonly IMarketManager _manager;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<MarketWrapper, LeagueKey> _markets;
    private readonly IRepository<Team, TeamKey> _teams;

    public MarketManagerRulesTest(
        IMarketManager manager,
        IRepository<Group, string> groups,
        IRepository<MarketWrapper, LeagueKey> markets,
        IRepository<Team, TeamKey> teams)
    {
        _manager = manager;
        _groups = groups;
        _markets = markets;
        _teams = teams;
    }

    public async ValueTask InitializeAsync()
        => await CleanupAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
        => await CleanupAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task DirectMarketWaitsForSellerAcceptanceBeforeExecuting()
    {
        await InsertContextAsync(MarketType.WithoutVote, 2, 100);

        var created = await _manager.CreateAsync(Key(), Proposal(5), Buyer, TestContext.Current.CancellationToken);

        Assert.True(created.IsOk);
        Assert.Equal(MarketStatus.Pending, created.Market!.Status);
        Assert.False(created.Market.SellerAccepted);
        Assert.Equal([Buyer], created.Market.Approvers);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);

        var result = await _manager.AcceptAsync(Key(), created.Market.Id, Seller, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Market!.SellerAccepted);
        Assert.Equal(MarketStatus.Approved, result.Market.Status);
        Assert.Equal(1, Assert.Single(result.Market.BuyerPlayers).Price);
        Assert.Equal(20, Assert.Single(result.Market.SellerPlayers).Price);
        var buyer = await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken);
        var seller = await _teams.GetAsync(TeamKey(Seller), TestContext.Current.CancellationToken);
        Assert.Equal("Seller player", Assert.Single(buyer!.ActivePlayers).Name);
        Assert.Equal("Buyer player", Assert.Single(seller!.ActivePlayers).Name);
        Assert.Equal(FantaSoccerRole.Forward, Assert.Single(buyer.ActivePlayers).Position);
        Assert.Equal(FantaSoccerRole.Tribune, Assert.Single(seller.ActivePlayers).Position);
        Assert.Equal(-5, buyer.MoneyFromRank);
        Assert.Equal(5, seller.MoneyFromRank);
    }

    [Fact]
    public async Task VotedMarketRequiresSellerAcceptanceBeforeLeagueVotes()
    {
        await InsertContextAsync(MarketType.WithVote, 4, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);
        Assert.Equal(MarketStatus.Pending, created.Market!.Status);
        Assert.False(created.Market.SellerAccepted);
        Assert.Equal([Buyer], created.Market.Approvers);

        var prematureVote = await _manager.ApproveAsync(Key(), created.Market.Id, "owner3@test.local", TestContext.Current.CancellationToken);
        Assert.False(prematureVote.IsOk);
        Assert.Equal(MarketStatus.Pending, prematureVote.Market!.Status);

        var accepted = await _manager.AcceptAsync(Key(), created.Market.Id, Seller, TestContext.Current.CancellationToken);
        Assert.True(accepted.IsOk);
        Assert.Equal(MarketStatus.Pending, accepted.Market!.Status);
        Assert.Equal([Buyer, Seller], accepted.Market.Approvers);

        var approved = await _manager.ApproveAsync(Key(), created.Market.Id, "owner3@test.local", TestContext.Current.CancellationToken);

        Assert.True(approved.IsOk);
        Assert.Equal(MarketStatus.Approved, approved.Market!.Status);
        Assert.Equal("Seller player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
    }

    [Fact]
    public async Task ReachingVotingQuorumDoesNotBypassSellerAcceptance()
    {
        await InsertContextAsync(MarketType.WithVote, 3, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        Assert.Equal(MarketStatus.Pending, created.Market!.Status);
        Assert.False(created.Market.SellerAccepted);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);

        var accepted = await _manager.AcceptAsync(Key(), created.Market.Id, Seller, TestContext.Current.CancellationToken);

        Assert.True(accepted.IsOk);
        Assert.True(accepted.Market!.SellerAccepted);
        Assert.Equal(MarketStatus.Approved, accepted.Market.Status);
    }

    [Fact]
    public async Task VotedMarketIsDeniedAtMajority()
    {
        await InsertContextAsync(MarketType.WithVote, 6, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);
        await _manager.AcceptAsync(Key(), created.Market!.Id, Seller, TestContext.Current.CancellationToken);

        for (var owner = 3; owner <= 6; owner++)
            await _manager.DenyAsync(Key(), created.Market.Id, $"owner{owner}@test.local", TestContext.Current.CancellationToken);

        var wrapper = await _markets.GetAsync(Key(), TestContext.Current.CancellationToken);
        Assert.Equal(MarketStatus.Denied, Assert.Single(wrapper!.Markets).Status);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
    }

    [Fact]
    public async Task DisabledMarketRejectsProposal()
    {
        await InsertContextAsync(MarketType.Denied, 2, 100);

        var result = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Null(await _markets.GetAsync(Key(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TradeWithInsufficientFinalBudgetDoesNotMovePlayers()
    {
        await InsertContextAsync(MarketType.WithoutVote, 2, 10);

        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);
        var result = await _manager.AcceptAsync(Key(), created.Market!.Id, Seller, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(MarketStatus.NoMoney, result.Market!.Status);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
        Assert.Equal("Seller player", Assert.Single((await _teams.GetAsync(TeamKey(Seller), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
    }

    [Fact]
    public async Task RejectsTradeWithDifferentRoleCounts()
    {
        await InsertContextAsync(MarketType.WithoutVote, 2, 100, sellerRole: Role.Midfielder);

        var result = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Null(await _markets.GetAsync(Key(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnlyBuyerOrSellerCanCancelPendingTrade()
    {
        await InsertContextAsync(MarketType.WithVote, 4, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        var unauthorized = await _manager.CancelAsync(Key(), created.Market!.Id, "owner3@test.local", TestContext.Current.CancellationToken);
        var cancelled = await _manager.CancelAsync(Key(), created.Market.Id, Seller, TestContext.Current.CancellationToken);

        Assert.False(unauthorized.IsOk);
        Assert.True(cancelled.IsOk);
        Assert.Equal(MarketStatus.Cancelled, cancelled.Market!.Status);
    }

    [Fact]
    public async Task MarksTradeWhenPlayerIsNoLongerAvailableAtQuorum()
    {
        await InsertContextAsync(MarketType.WithVote, 4, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);
        await _manager.AcceptAsync(Key(), created.Market!.Id, Seller, TestContext.Current.CancellationToken);
        var seller = await _teams.GetAsync(TeamKey(Seller), TestContext.Current.CancellationToken);
        seller!.Players.Clear();
        await _teams.UpdateAsync(TeamKey(Seller), seller, TestContext.Current.CancellationToken);

        var result = await _manager.ApproveAsync(Key(), created.Market.Id, "owner3@test.local", TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(MarketStatus.NoPlayers, result.Market!.Status);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
    }

    [Fact]
    public async Task CountsRankMoneyOnceWhenCheckingBudget()
    {
        await InsertContextAsync(MarketType.WithoutVote, 2, 100);
        var buyer = await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken);
        buyer!.MoneyFromRank = 20;
        await _teams.UpdateAsync(TeamKey(Buyer), buyer, TestContext.Current.CancellationToken);

        var created = await _manager.CreateAsync(Key(), Proposal(111), Buyer, TestContext.Current.CancellationToken);
        var result = await _manager.AcceptAsync(Key(), created.Market!.Id, Seller, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(MarketStatus.NoMoney, result.Market!.Status);
    }

    [Fact]
    public async Task SellerCanRejectProposalWithoutMovingPlayers()
    {
        await InsertContextAsync(MarketType.WithoutVote, 2, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        var rejected = await _manager.RejectAsync(Key(), created.Market!.Id, Seller, TestContext.Current.CancellationToken);

        Assert.True(rejected.IsOk);
        Assert.Equal(MarketStatus.Denied, rejected.Market!.Status);
        Assert.False(rejected.Market.SellerAccepted);
        Assert.Equal("Buyer player", Assert.Single((await _teams.GetAsync(TeamKey(Buyer), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
        Assert.Equal("Seller player", Assert.Single((await _teams.GetAsync(TeamKey(Seller), TestContext.Current.CancellationToken))!.ActivePlayers).Name);
    }

    [Fact]
    public async Task OnlySellerCanAcceptOrRejectProposal()
    {
        await InsertContextAsync(MarketType.WithVote, 4, 100);
        var created = await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        var buyerAccept = await _manager.AcceptAsync(Key(), created.Market!.Id, Buyer, TestContext.Current.CancellationToken);
        var otherAccept = await _manager.AcceptAsync(Key(), created.Market.Id, "owner3@test.local", TestContext.Current.CancellationToken);
        var buyerReject = await _manager.RejectAsync(Key(), created.Market.Id, Buyer, TestContext.Current.CancellationToken);

        Assert.False(buyerAccept.IsOk);
        Assert.False(otherAccept.IsOk);
        Assert.False(buyerReject.IsOk);
        var saved = Assert.Single((await _markets.GetAsync(Key(), TestContext.Current.CancellationToken))!.Markets);
        Assert.Equal(MarketStatus.Pending, saved.Status);
        Assert.False(saved.SellerAccepted);
    }

    [Fact]
    public async Task MarketHistoryIsVisibleOnlyToLeagueMembers()
    {
        await InsertContextAsync(MarketType.WithVote, 4, 100);
        await _manager.CreateAsync(Key(), Proposal(), Buyer, TestContext.Current.CancellationToken);

        Assert.NotNull(await _manager.GetAsync(Key(), Buyer, TestContext.Current.CancellationToken));
        Assert.Null(await _manager.GetAsync(Key(), "outsider@test.local", TestContext.Current.CancellationToken));
    }

    private async Task InsertContextAsync(
        MarketType marketType,
        int teamCount,
        int startingMoney,
        Role buyerRole = Role.Forward,
        Role sellerRole = Role.Forward)
    {
        var owners = new[] { Buyer, Seller }
            .Concat(Enumerable.Range(3, Math.Max(0, teamCount - 2)).Select(x => $"owner{x}@test.local"))
            .ToList();
        var settings = LeagueSetting.Default;
        settings.Market = marketType;
        settings.StartingMoney = startingMoney;
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
                            Teams = owners.Select(owner => new AnnualTeam
                            {
                                Name = owner,
                                Owner = owner,
                                AdditionalOwners = []
                            }).ToList()
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(Buyer), Team(Buyer, "Buyer player", 1, buyerRole, FantaSoccerRole.Forward), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(Seller), Team(Seller, "Seller player", 20, sellerRole, FantaSoccerRole.Tribune), TestContext.Current.CancellationToken);
    }

    private static Market Proposal(int buyerMoney = 0)
        => new()
        {
            Buyer = Buyer,
            Seller = Seller,
            BuyerPlayers = [Player("Buyer player", 999)],
            SellerPlayers = [Player("Seller player", 999)],
            MoneyFromBuyer = buyerMoney,
            MoneyFromSeller = 0,
            Approvers = [],
            Deniers = []
        };

    private static Team Team(string owner, string playerName, int price, Role role, FantaSoccerRole position)
        => new()
        {
            Name = owner,
            Owner = owner,
            AdditionalOwners = [],
            Players = [Player(playerName, price, role, position)]
        };

    private static Player Player(
        string name,
        int price,
        Role role = Role.Forward,
        FantaSoccerRole position = FantaSoccerRole.Tribune)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = "Roma" },
            Role = role,
            Position = position,
            Status = PlayerInTeamStatus.Active,
            Price = price
        };

    private static LeagueKey Key()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear };

    private static TeamKey TeamKey(string owner)
        => new() { Group = GroupId, BasketId = BasketId, Email = owner, Year = FantacalcioTime.ActualYear };

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _markets.DeleteAsync(Key(), cancellationToken);
        await _teams.DeleteAsync(TeamKey(Buyer), cancellationToken);
        await _teams.DeleteAsync(TeamKey(Seller), cancellationToken);
    }
}
