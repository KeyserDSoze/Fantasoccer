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

        public SerieAJob(IFantasoccerLogger logger, ISerieAInformation serieA,
            IRepository<RealCalendar, int> repository,
            IRepository<RealRank, int> rankRepository)
        {
            _logger = logger;
            _serieA = serieA;
            _repository = repository;
            _rankRepository = rankRepository;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var result = false;
            if (FantacalcioTime.ItalyNow.Hour > 11 && FantacalcioTime.ItalyNow.Hour <= 23)
            {
                if (await _repository.ExistAsync(FantacalcioTime.ActualYear).NoContext())
                {
                    var calendar = (await _repository.GetAsync(FantacalcioTime.ActualYear))!;
                    var lastDay = calendar.LastDay?.SerieADay ?? calendar.LiveDay?.SerieADay ?? 39;
                    if (lastDay <= 38)
                    {
                        var partOfTheRealCalendar = await _serieA.GetCalendarAsync(lastDay, lastDay + 3);
                        foreach (var actualDay in partOfTheRealCalendar.Days)
                        {
                            calendar.Days.Remove(calendar.Days.Find(x => x.SerieADay == actualDay.SerieADay)!);
                            calendar.Days.Add(actualDay);
                        }
                        calendar.Days = [.. calendar.Days.OrderBy(x => x.SerieADay)];
                        result = await _repository.UpdateAsync(FantacalcioTime.ActualYear, calendar).NoContext();
                    }
                }
                else
                {
                    var calendar = await _serieA.GetCalendarAsync().NoContext();
                    result = await _repository.InsertAsync(FantacalcioTime.ActualYear, calendar).NoContext();
                }
                var rank = await _serieA.GetRankAsync().NoContext();
                if (await _rankRepository.ExistAsync(FantacalcioTime.ActualYear))
                {
                    await _rankRepository.UpdateAsync(FantacalcioTime.ActualYear, rank);
                }
                else
                {
                    await _rankRepository.InsertAsync(FantacalcioTime.ActualYear, rank);
                }
                await _logger.LogAsync<SerieAJob>($"Giornate parsate e salvate con risultato {result}.").NoContext();
            }
        }

        public Task OnException(Exception exception)
             => _logger.LogErrorAsync<SerieAJob>(exception);
    }
}
