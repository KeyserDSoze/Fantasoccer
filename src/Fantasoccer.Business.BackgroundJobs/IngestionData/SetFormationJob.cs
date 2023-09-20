using System.Timers;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class SetFormationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Team, TeamDayKey> _repository;
        private readonly IRepository<RealCalendar, int> _calendar;
        private readonly IRepository<Group, string> _groups;

        public SetFormationJob(IFantasoccerLogger logger,
            IRepository<Team, TeamDayKey> repository,
            IRepository<RealCalendar, int> calendar,
            IRepository<Group, string> groups)
        {
            _logger = logger;
            _repository = repository;
            _calendar = calendar;
            _groups = groups;
        }

        public async Task ActionToDoAsync()
        {
            var calendar = await _calendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            if (calendar != null && calendar.LastDay != null && calendar.LastDay.SerieADay < 38)
            {
                var listUpdated = new List<string>();
                var groups = await _groups.ToListAsEntityAsync().NoContext();
                foreach (var group in groups.Where(x => x.Baskets != null))
                {
                    foreach (var basket in group.Baskets.Where(x => x.Years != null))
                    {
                        foreach (var owner in basket.Years
                            .Where(x => x.Year == FantacalcioTime.ActualYear)
                            .SelectMany(x => x.Teams)
                            .Select(x => x.Owner))
                        {
                            var nextKey = new TeamDayKey
                            {
                                Year = FantacalcioTime.ActualYear,
                                Day = calendar.LastDay.SerieADay + 1,
                                Email = owner,
                                Group = group.Id,
                                BasketId = basket.Id
                            };
                            if (!await _repository.ExistAsync(nextKey))
                            {
                                var key = new TeamDayKey
                                {
                                    Year = FantacalcioTime.ActualYear,
                                    Day = calendar.LastDay.SerieADay,
                                    Email = owner,
                                    Group = group.Id,
                                    BasketId = basket.Id
                                };
                                var oldTeam = await _repository.GetAsync(key).NoContext();
                                if (oldTeam != null)
                                {
                                    await _repository.UpdateAsync(nextKey, oldTeam).NoContext();
                                    listUpdated.Add(owner);
                                }
                                else
                                    await _logger.LogAsync<SetFormationJob>($"Impossible to set formation for {owner}").NoContext();
                            }
                        }
                    }
                }
                await _logger.LogAsync<SetFormationJob>($"Formations set for {string.Join(',', listUpdated)}").NoContext();
            }
            else
                await _logger.LogAsync<SetFormationJob>($"Calendar nullo.").NoContext();
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<SetFormationJob>(exception);
    }
}
