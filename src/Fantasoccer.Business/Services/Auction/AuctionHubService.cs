using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Concurrent;
using Fantasoccer.Domain;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    public sealed class AuctionHubService
    {
        private readonly ConcurrentDictionary<string, AuctionHubWrapper> _auctions = new();
        private readonly ILock _lock;
        private readonly IRepository<AuctionWrapper, string> _auctionRepository;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IRepository<RealCalendar, int> _realCalendarRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<Team, TeamKey> _team;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<StatPlayersWrapper, int> _statsPlayers;

        public AuctionHubService(ILock @lock, IRepository<AuctionWrapper, string> auctionRepository,
             IRepository<AppIdentity, string> identityRepository,
             IRepository<RealCalendar, int> realCalendarRepository,
            IRepository<Rank, LeagueKey> rankRepository,
           IRepository<Team, TeamKey> team, IRepository<Group, string> groups, IRepository<StatPlayersWrapper, int> statsPlayers)
        {
            _lock = @lock;
            _auctionRepository = auctionRepository;
            _identityRepository = identityRepository;
            _realCalendarRepository = realCalendarRepository;
            _rankRepository = rankRepository;
            _team = team;
            _groups = groups;
            _statsPlayers = statsPlayers;
        }
        public async ValueTask ExecuteOperationAsync(string auctionId, Action<AuctionHubWrapper> concurrentAction)
        {
            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper != null)
            {
                await _lock.ExecuteAsync(() =>
                {
                    concurrentAction(wrapper);
                    return Task.CompletedTask;
                }, $"actions_{auctionId}");
            }
        }
        public async Task<TResult?> ExecuteOperationAsync<TResult>(string auctionId, Func<AuctionHubWrapper, Task<TResult>> concurrentFunc)
        {
            TResult? result = default;
            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper != null)
            {
                await _lock.ExecuteAsync(async () =>
                {
                    result = await concurrentFunc(wrapper);
                }, $"actions_{auctionId}");
            }
            return result;
        }
        public async Task<AuctionHubWrapper?> GetCurrentWrapperAsync(string auctionId)
        {
            var added = false;
            if (!_auctions.ContainsKey(auctionId))
            {
                await _lock.ExecuteAsync(async () =>
                {
                    if (!_auctions.ContainsKey(auctionId))
                    {
                        var auction = await _auctionRepository.GetAsync(auctionId);
                        if (auction == null)
                            throw new InvalidOperationException("Auction not found");
                        var group = await _groups.GetAsync(auction.LeagueKey.Group);
                        if (group != null)
                        {
                            var league = group.GetAnnualLeague(auction.LeagueKey.League, auction.LeagueKey.Year);
                            var leagueWrapper = group.Leagues.FirstOrDefault(x => x.Id == auction.LeagueKey.League);
                            if (league != null && leagueWrapper != null)
                            {
                                var calendar = await _realCalendarRepository.GetAsync(auction.LeagueKey.Year);
                                var rank = await _rankRepository.GetAsync(auction.LeagueKey);
                                var statPlayers = await _statsPlayers.GetAsync(auction.LeagueKey.Year);
                                var teams = new List<Entity<Team, TeamKey>>();
                                foreach (var basketId in leagueWrapper.BasketsId)
                                {
                                    var basket = group.Baskets.First(x => x.Id == basketId);
                                    var theYear = basket.Years.First(x => x.Year == auction.LeagueKey.Year);
                                    foreach (var teamInYear in theYear.Teams)
                                    {
                                        var teamKey = new TeamKey
                                        {
                                            Group = auction.LeagueKey.Group,
                                            BasketId = basketId,
                                            Year = auction.LeagueKey.Year,
                                            Email = teamInYear.Owner
                                        };
                                        var team = _team.GetAsync(teamKey).ToResult();
                                        if (team == null)
                                        {
                                            team = new() { Name = teamInYear.Name, Owner = teamInYear.Owner, AdditionalOwners = teamInYear.AdditionalOwners ?? [], Players = [] };
                                            _team.UpdateAsync(teamKey, team).ToResult();
                                        }
                                        else
                                        {
                                            team.AdditionalOwners = teamInYear.AdditionalOwners ?? [];
                                        }
                                        var teamInRank = rank?.RankedTeams.SelectMany(x => x.Value).FirstOrDefault(x => x.Owner == teamInYear.Owner);
                                        if (teamInRank != null && (league.Settings.MoneyForGoal > 0 || league.Settings.MoneyForSufferedGoal > 0))
                                        {
                                            team.MoneyFromRank = teamInRank.Goal * league.Settings.MoneyForGoal + teamInRank.SufferedGoal * league.Settings.MoneyForSufferedGoal;
                                        }
                                        teams.Add(new(team, teamKey));
                                    }
                                }
                                added = _auctions.TryAdd(auctionId, new AuctionHubWrapper
                                {
                                    Wrapper = auction,
                                    LastUpdate = DateTime.UtcNow,
                                    Group = group,
                                    League = league,
                                    LeagueName = leagueWrapper?.Name ?? auction.LeagueKey.League,
                                    RealPlayers = statPlayers?.Players ?? [],
                                    RealCalendar = calendar,
                                    Teams = teams,
                                    LastJsonSaved = auction.ToJson()
                                });
                            }
                        }
                    }
                }, auctionId);
            }
            _auctions.TryGetValue(auctionId, out var wrapper);
            if (added && wrapper != null)
            {
                _ = UpdateAsync(auctionId);
            }
            return wrapper;
        }
        private async ValueTask UpdateAsync(string auctionId)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            if (_auctions.TryGetValue(auctionId, out var wrapper))
            {
                await _lock.ExecuteAsync(async () =>
                {
                    var json = wrapper.Wrapper.ToJson();
                    await Try.WithDefaultOnCatchAsync(async () =>
                    {
                        if (json != wrapper.LastJsonSaved)
                        {
                            wrapper.LastJsonSaved = json;
                            await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);
                        }
                        if (DateTime.UtcNow.AddDays(-2) > wrapper.Wrapper.Created)
                        {
                            wrapper.Wrapper.Status = AuctionStatus.Finished;
                            await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);
                        }
                        if (wrapper.Wrapper.Status != AuctionStatus.Finished)
                        {
                            _ = UpdateAsync(auctionId);
                        }
                    });
                }, $"actions_{auctionId}");
            }
        }
    }
}
