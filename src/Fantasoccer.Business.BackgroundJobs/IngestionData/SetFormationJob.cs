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

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var calendar = await _calendar.GetAsync(actualYear, cancellationToken).NoContext();
            var day = calendar?.LiveDay?.SerieADay ?? calendar?.LastDay?.SerieADay ?? 38;
            if (day < 38)
            {
                var listUpdated = new List<string>();
                var groups = await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken).NoContext();
                foreach (var group in groups)
                {
                    if (string.IsNullOrWhiteSpace(group.Id))
                        continue;

                    foreach (var basket in group.Baskets ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(basket.Id))
                            continue;

                        foreach (var owner in basket.Years?
                            .Where(x => x.Year == actualYear)
                            .SelectMany(x => x.Teams ?? [])
                            .Select(x => x.Owner)
                            .Where(x => !string.IsNullOrWhiteSpace(x)) ?? [])
                        {
                            var nextKey = new TeamDayKey
                            {
                                Year = actualYear,
                                Day = day + 1,
                                Email = owner,
                                Group = group.Id,
                                BasketId = basket.Id
                            };
                            if (!await _repository.ExistAsync(nextKey, cancellationToken))
                            {
                                var key = new TeamDayKey
                                {
                                    Year = actualYear,
                                    Day = day,
                                    Email = owner,
                                    Group = group.Id,
                                    BasketId = basket.Id
                                };
                                var oldTeam = await _repository.GetAsync(key, cancellationToken).NoContext();
                                if (oldTeam != null)
                                {
                                    await _repository.InsertAsync(nextKey, oldTeam, cancellationToken).NoContext();
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
