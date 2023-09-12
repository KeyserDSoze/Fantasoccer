using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public record CalendarContext(string Group, int Year, string League)
    {
        public Task SaveAsync(Calendar calendar)
          => FantacalcioContext.SaveAsync(FantacalcioId.GetCalendar(Group, Year, League), calendar);
        public Task<Calendar> GetAsync()
           => FantacalcioContext.GetAsync<Calendar>(FantacalcioId.GetCalendar(Group, Year, League));
    }
}