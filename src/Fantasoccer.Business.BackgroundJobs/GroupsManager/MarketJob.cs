using System.Timers;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager
{
    public sealed class MarketJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<MarketWrapper, LeagueKey> _marketRepository;

        public MarketJob(IFantasoccerLogger logger,
            IRepository<Group, string> groups,
            IRepository<MarketWrapper, LeagueKey> marketRepository)
        {
            _logger = logger;
            _groups = groups;
            _marketRepository = marketRepository;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var groups = await _groups.ToListAsEntityAsync();
            foreach (var group in groups)
            {
                foreach (var league in group.Leagues?.Where(x => x.Years?.Exists(t => t.Year == actualYear) == true) ?? [])
                {
                    var leagueKey = new LeagueKey { Group = group.Id, League = league.Id, Year = actualYear };
                    var market = await _marketRepository.GetAsync(leagueKey).NoContext();
                    if (market == null)
                    {
                        market = new() { Markets = new() };
                        await _marketRepository.InsertAsync(leagueKey, market);
                    }
                    market.Markets ??= [];
                    var hasExpiredSomething = false;
                    foreach (var exchange in market.Markets.Where(x => x.Status == MarketStatus.Pending))
                    {
                        if (DateTime.UtcNow.Subtract(exchange.CreationTime) > TimeSpan.FromDays(14))
                        {
                            exchange.Status = MarketStatus.Expired;
                            hasExpiredSomething = true;
                        }
                    }
                    if (hasExpiredSomething)
                        await _marketRepository.UpdateAsync(leagueKey, market);
                }
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<MarketJob>(exception);
    }
}
