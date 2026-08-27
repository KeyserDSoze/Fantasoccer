using System.Timers;
using Fantasoccer.Business;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public sealed class SerieAJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ISerieAInformation _serieA;
        private readonly IRepository<RealCalendar, int> _repository;
        private readonly IRepository<RealRank, int> _rankRepository;
        private readonly TimeProvider _timeProvider;

        public SerieAJob(IFantasoccerLogger logger, ISerieAInformation serieA,
            IRepository<RealCalendar, int> repository,
            IRepository<RealRank, int> rankRepository,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _serieA = serieA;
            _repository = repository;
            _rankRepository = rankRepository;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var result = false;
            var year = FantacalcioTime.ActualYear;
            var italyNow = FantacalcioTime.ToItalyNow(_timeProvider.GetUtcNow().UtcDateTime);
            if (italyNow.Hour > 11 && italyNow.Hour <= 23)
            {
                var calendar = await _repository.GetAsync(year, cancellationToken).NoContext();
                if (calendar?.Days?.Count > 0)
                {
                    var lastDay = calendar.LastDay?.SerieADay ?? GetLiveDay(calendar.Days, italyNow)?.SerieADay ?? 39;
                    if (lastDay <= 38)
                    {
                        var partOfTheRealCalendar = await _serieA.GetCalendarAsync(lastDay, lastDay + 3);
                        var updatedDays = partOfTheRealCalendar?.Days?.Where(x => x?.Games?.Count > 0).ToList() ?? [];
                        foreach (var actualDay in updatedDays)
                        {
                            var serieADay = calendar.Days.Find(x => x.SerieADay == actualDay.SerieADay);
                            if (serieADay != null)
                            {
                                calendar.Days.Remove(serieADay);
                                foreach (var game in serieADay.Games?.Where(x => x.Delayed) ?? [])
                                {
                                    var theActualGame = actualDay.Games.FirstOrDefault(x =>
                                        x.Home?.Name == game.Home?.Name && x.Away?.Name == game.Away?.Name);
                                    if (theActualGame != null)
                                    {
                                        theActualGame.Delayed = true;
                                    }
                                }
                            }
                            calendar.Days.Add(actualDay);
                        }
                        if (updatedDays.Count > 0)
                        {
                            calendar.Days = [.. calendar.Days.OrderBy(x => x.SerieADay)];
                            result = await _repository.UpdateAsync(year, calendar, cancellationToken).NoContext();
                        }
                    }
                }
                else
                {
                    calendar = await _serieA.GetCalendarAsync().NoContext();
                    if (calendar?.Days?.Count > 0)
                        result = await _repository.InsertAsync(year, calendar, cancellationToken).NoContext();
                }
                var rank = calendar == null ? null : BuildRank(calendar, italyNow);
                if (rank?.Teams.Count > 0 && await _rankRepository.ExistAsync(year, cancellationToken))
                {
                    await _rankRepository.UpdateAsync(year, rank, cancellationToken);
                }
                else if (rank?.Teams.Count > 0)
                {
                    await _rankRepository.InsertAsync(year, rank, cancellationToken);
                }
                await _logger.LogAsync<SerieAJob>($"Giornate parsate e salvate con risultato {result}.").NoContext();
            }
        }

        private static RealDay? GetLiveDay(IEnumerable<RealDay> days, DateTime italyNow)
            => days.FirstOrDefault(day => day.Games?.Any(game =>
                !game.Delayed && game.Date.HasValue && italyNow >= game.Date.Value && italyNow <= game.Date.Value.AddHours(10.25)) == true);

        private static RealRank BuildRank(RealCalendar calendar, DateTime italyNow)
        {
            var teams = new Dictionary<string, RealRankedTeam>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in calendar.Days?.SelectMany(x => x.Games ?? []) ?? [])
            {
                if (string.IsNullOrWhiteSpace(game.Home?.Name) || string.IsNullOrWhiteSpace(game.Away?.Name))
                    continue;

                if (!teams.TryGetValue(game.Home.Name, out var home))
                {
                    home = new RealRankedTeam { Name = game.Home.Name };
                    teams.Add(home.Name, home);
                }
                if (!teams.TryGetValue(game.Away.Name, out var away))
                {
                    away = new RealRankedTeam { Name = game.Away.Name };
                    teams.Add(away.Name, away);
                }

                if (game.Delayed || !game.Date.HasValue || game.Date.Value > italyNow ||
                    !game.HomeGoal.HasValue || !game.AwayGoal.HasValue)
                    continue;

                var homeGoal = game.HomeGoal.Value;
                var awayGoal = game.AwayGoal.Value;
                home.Played++;
                away.Played++;
                home.Goal += homeGoal;
                home.SufferedGoal += awayGoal;
                away.Goal += awayGoal;
                away.SufferedGoal += homeGoal;

                if (homeGoal > awayGoal)
                {
                    home.Victories++;
                    away.Defeats++;
                    home.Point += 3;
                }
                else if (homeGoal < awayGoal)
                {
                    away.Victories++;
                    home.Defeats++;
                    away.Point += 3;
                }
                else
                {
                    home.Draws++;
                    away.Draws++;
                    home.Point++;
                    away.Point++;
                }
            }

            return new RealRank
            {
                Teams =
                [
                    .. teams.Values
                        .OrderByDescending(x => x.Point)
                        .ThenByDescending(x => x.Goal - x.SufferedGoal)
                        .ThenByDescending(x => x.Goal)
                        .ThenBy(x => x.SufferedGoal)
                        .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                ]
            };
        }

        public Task OnException(Exception exception)
             => _logger.LogErrorAsync<SerieAJob>(exception);
    }
}
