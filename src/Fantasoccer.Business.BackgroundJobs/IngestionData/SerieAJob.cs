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
                var rank = await _serieA.GetRankAsync().NoContext();
                if (rank?.Teams?.Count > 0 && await _rankRepository.ExistAsync(year, cancellationToken))
                {
                    await _rankRepository.UpdateAsync(year, rank, cancellationToken);
                }
                else if (rank?.Teams?.Count > 0)
                {
                    await _rankRepository.InsertAsync(year, rank, cancellationToken);
                }
                await _logger.LogAsync<SerieAJob>($"Giornate parsate e salvate con risultato {result}.").NoContext();
            }
        }

        private static RealDay? GetLiveDay(IEnumerable<RealDay> days, DateTime italyNow)
            => days.FirstOrDefault(day => day.Games?.Any(game =>
                !game.Delayed && game.Date.HasValue && italyNow >= game.Date.Value && italyNow <= game.Date.Value.AddHours(10.25)) == true);

        public Task OnException(Exception exception)
             => _logger.LogErrorAsync<SerieAJob>(exception);
    }
}
