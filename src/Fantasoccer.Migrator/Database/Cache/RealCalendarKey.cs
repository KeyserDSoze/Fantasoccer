using Rystem.Business;
using System.Threading.Tasks;
using FantacalcioV4.Database;

namespace FantacalcioV4
{
    public class RealCalendarKey : ICacheKey<RealCalendar>
    {
        public int Year { get; set; } = FantacalcioTime.ActualYear;
        public Task<RealCalendar> FetchAsync() 
            => new SerieACalendarContext(Year).GetAsync();
    }
}