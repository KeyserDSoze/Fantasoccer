using System.Threading.Tasks;
using System;

namespace FantacalcioV4.Database
{
    public record SerieACalendarContext(int Year)
    {
        public Task SaveAsync(RealCalendar calendar)
            => Task.WhenAll(FantacalcioContext.SaveAsync(FantacalcioId.GetSerieACalendar(Year), calendar),
                new RealCalendarKey { Year = Year }.RestoreAsync(calendar));
        public Task<RealCalendar> GetAsync()
            => FantacalcioContext.GetAsync<RealCalendar>(FantacalcioId.GetSerieACalendar(Year));
    }
}