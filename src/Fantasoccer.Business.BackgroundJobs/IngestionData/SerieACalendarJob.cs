using System.Text.Json.Serialization;
using System.Timers;
using Fantasoccer.Business;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public sealed class SerieACalendarJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ISerieACalendar _calendar;
        private readonly IRepository<RealCalendar, int> _repository;
        public SerieACalendarJob(IFantasoccerLogger logger, ISerieACalendar calendar, IRepository<RealCalendar, int> repository)
        {
            _logger = logger;
            _calendar = calendar;
            _repository = repository;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var result = false;
            if (await _repository.ExistAsync(FantacalcioTime.ActualYear).NoContext())
            {
                var calendar = (await _repository.GetAsync(FantacalcioTime.ActualYear))!;
                var lastDay = calendar.LastDay?.SerieADay ?? calendar.LiveDay?.SerieADay ?? 39;
                if (lastDay <= 38)
                {
                    var partOfTheRealCalendar = await _calendar.GetAsync(lastDay, lastDay + 3);
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
                var calendar = await _calendar.GetAsync().NoContext();
                result = await _repository.InsertAsync(FantacalcioTime.ActualYear, calendar).NoContext();
            }
            await _logger.LogAsync<SerieACalendarJob>($"Giornate parsate e salvate con risultato {result}.").NoContext();
        }

        public Task OnException(Exception exception)
             => _logger.LogErrorAsync<SerieACalendarJob>(exception);
    }
}
