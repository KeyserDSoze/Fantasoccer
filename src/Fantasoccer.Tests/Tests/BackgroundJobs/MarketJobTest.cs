using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

public sealed class MarketJobTest
{
    private readonly MarketJob _marketJob;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<MarketWrapper, LeagueKey> _markets;

    public MarketJobTest(
        MarketJob marketJob,
        IRepository<Group, string> groups,
        IRepository<MarketWrapper, LeagueKey> markets)
    {
        _marketJob = marketJob;
        _groups = groups;
        _markets = markets;
    }

    [Fact]
    public async Task IgnoresGroupWithoutLeagues()
    {
        var groupId = Guid.NewGuid().ToString();
        try
        {
            await _groups.InsertAsync(groupId, new Group
            {
                Id = groupId,
                Name = "Group without leagues",
                Leagues = null!,
                Users = [],
                Baskets = []
            });

            await _marketJob.ActionToDoAsync(default);
        }
        finally
        {
            await _groups.DeleteAsync(groupId);
        }
    }

    [Fact]
    public async Task CreatesMarketForCurrentYearLeague()
    {
        var groupId = Guid.NewGuid().ToString();
        var leagueId = Guid.NewGuid().ToString();
        try
        {
            await _groups.InsertAsync(groupId, CreateGroup(groupId, leagueId));

            await _marketJob.ActionToDoAsync(default);

            var market = await _markets.GetAsync(new LeagueKey
            {
                Group = groupId,
                League = leagueId,
                Year = FantacalcioTime.ActualYear
            });
            Assert.NotNull(market);
            Assert.NotNull(market.Markets);
        }
        finally
        {
            await _groups.DeleteAsync(groupId);
            await _markets.DeleteAsync(new LeagueKey
            {
                Group = groupId,
                League = leagueId,
                Year = FantacalcioTime.ActualYear
            });
        }
    }

    [Fact]
    public async Task ExpiresPendingMarketsOlderThanFourteenDays()
    {
        var groupId = Guid.NewGuid().ToString();
        var leagueId = Guid.NewGuid().ToString();
        var key = new LeagueKey
        {
            Group = groupId,
            League = leagueId,
            Year = FantacalcioTime.ActualYear
        };
        try
        {
            await _groups.InsertAsync(groupId, CreateGroup(groupId, leagueId));
            await _markets.InsertAsync(key, new MarketWrapper
            {
                Markets =
                [
                    new Market
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = MarketStatus.Pending,
                        CreationTime = DateTime.UtcNow.AddDays(-15)
                    }
                ]
            });

            await _marketJob.ActionToDoAsync(default);

            var market = await _markets.GetAsync(key);
            Assert.NotNull(market);
            Assert.Equal(MarketStatus.Expired, Assert.Single(market.Markets).Status);
        }
        finally
        {
            await _groups.DeleteAsync(groupId);
            await _markets.DeleteAsync(key);
        }
    }

    private static Group CreateGroup(string groupId, string leagueId)
        => new()
        {
            Id = groupId,
            Name = "Market test group",
            Leagues =
            [
                new League
                {
                    Id = leagueId,
                    Name = "Market test league",
                    Years = [new AnnualLeague
                    {
                        Year = FantacalcioTime.ActualYear,
                        Settings = LeagueSetting.Default
                    }],
                    BasketsId = []
                }
            ],
            Users = [],
            Baskets = []
        };
}