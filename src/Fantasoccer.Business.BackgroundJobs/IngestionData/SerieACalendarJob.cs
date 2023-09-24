using System.Timers;
using Fantasoccer.Business;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class SerieACalendarJob : IBackgroundJob
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

        public async Task ActionToDoAsync()
        {
            var calendar = await _calendar.GetAsync().NoContext();
            bool result;
            if (await _repository.ExistAsync(FantacalcioTime.ActualYear).NoContext())
            {
                result = await _repository.UpdateAsync(FantacalcioTime.ActualYear, calendar).NoContext();
            }
            else
            {
                result = await _repository.InsertAsync(FantacalcioTime.ActualYear, calendar).NoContext();
            }
            await _logger.LogAsync<SerieACalendarJob>($"Giornate {calendar.Days.Count} parsate e salvate con risultato {result}.").NoContext();
        }

        public Task OnException(Exception exception)
             => _logger.LogErrorAsync<SerieACalendarJob>(exception);
    }
}
