using System.Timers;
using Fantasoccer.Business;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class SetFormationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Team, TeamDayKey> _repository;
        private readonly IRepository<RealCalendar, int> _calendar;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Settings, string>? _settings;
        private readonly IOptimalFormationService? _optimalFormationService;

        public SetFormationJob(IFantasoccerLogger logger,
            IRepository<Team, TeamDayKey> repository,
            IRepository<RealCalendar, int> calendar,
            IRepository<Group, string> groups,
            IRepository<Settings, string>? settings = null,
            IOptimalFormationService? optimalFormationService = null)
        {
            _logger = logger;
            _repository = repository;
            _calendar = calendar;
            _groups = groups;
            _settings = settings;
            _optimalFormationService = optimalFormationService;
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
                                    // La formazione passa alla nuova giornata, i cambi live e il relativo storico no.
                                    var nextTeam = oldTeam.Clone();
                                    nextTeam.FormationChanges = 0;
                                    nextTeam.FormationChangeHistory = [];
                                    var userSettings = _settings == null
                                        ? null
                                        : await _settings.GetAsync(owner, cancellationToken).NoContext();
                                    if (userSettings?.AutoOptimalFormation == true && _optimalFormationService != null)
                                        await ApplyOptimalFormationAsync(group, basket.Id, actualYear, day + 1, nextTeam, cancellationToken);
                                    await _repository.InsertAsync(nextKey, nextTeam, cancellationToken).NoContext();
                                    listUpdated.Add(owner);
                                }
                                else
                                    await _logger.LogAsync<SetFormationJob>($"Impossible to set formation for {owner}").NoContext();
                            }
                            else
                            {
                                var nextTeam = await _repository.GetAsync(nextKey, cancellationToken).NoContext();
                                if (nextTeam != null && (nextTeam.FormationChanges > 0 || (nextTeam.FormationChangeHistory?.Count ?? 0) > 0))
                                {
                                    nextTeam = nextTeam.Clone();
                                    nextTeam.FormationChanges = 0;
                                    nextTeam.FormationChangeHistory = [];
                                    await _repository.UpdateAsync(nextKey, nextTeam, cancellationToken).NoContext();
                                }
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

        private async Task ApplyOptimalFormationAsync(Group group, string basketId, int year, int day, Team team, CancellationToken cancellationToken)
        {
            var annualLeague = group.Leagues
                .Where(x => x.BasketsId.Contains(basketId, StringComparer.OrdinalIgnoreCase))
                .SelectMany(x => x.Years.Where(y => y.Year == year).Select(y => (League: x, Annual: y)))
                .FirstOrDefault();
            if (annualLeague.Annual == null)
                return;

            var leagueType = annualLeague.Annual.Type == LeagueType.Null
                ? (annualLeague.League.Type == LeagueType.Null ? LeagueType.League : annualLeague.League.Type)
                : annualLeague.Annual.Type;
            var result = await _optimalFormationService.CalculateAsync(
                team,
                year,
                day,
                leagueType,
                annualLeague.Annual.Settings ?? LeagueSetting.Default,
                cancellationToken);
            var positions = result.Players.ToDictionary(x => x.PlayerKey, x => x.Position, StringComparer.OrdinalIgnoreCase);
            foreach (var player in team.ActivePlayers)
                if (positions.TryGetValue(player.Key, out var position))
                    player.Position = position;
        }
    }
}
