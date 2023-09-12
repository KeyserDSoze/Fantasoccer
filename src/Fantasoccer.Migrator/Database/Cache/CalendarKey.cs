using Rystem.Business;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class CalendarKey : ICacheKey<Calendar>
    {
        public int Year { get; set; }
        public string GroupName { get; set; }
        public string LeagueName { get; set; }
        public async Task<Calendar> FetchAsync()
        {
            var group = await new GroupKey { Name = GroupName }.InstanceAsync();
            var settings = group.Leagues.First(x => x.Name == LeagueName).Years.FirstOrDefault(x => x.Year == Year).Settings;
            var calendar = await new CalendarContext(GroupName, Year, LeagueName).GetAsync();
            RealCalendar realCalendar = await new RealCalendarKey { Year = Year }.InstanceAsync();
            int maxSerieADay = realCalendar.LastDay.SerieADay;
            foreach (var day in calendar.Days)
            {
                if (day != default && day.SerieADay <= maxSerieADay)
                {
                    var votes = await new FinalVotesContext(Year, day.SerieADay).GetAsync();
                    if (votes != default)
                        day.SetVote(votes, settings);
                }
            }
            return calendar;
        }
    }
}