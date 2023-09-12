using Rystem.Business;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class RankKey : ICacheKey<Rank>
    {
        public int Year { get; set; }
        public string GroupName { get; set; }
        public string LeagueName { get; set; }
        public async Task<Rank> FetchAsync()
        {
            var group = await new GroupKey { Name = GroupName }.InstanceAsync();
            if (!group.Leagues.FirstOrDefault(x => x.Name == LeagueName).Years.Any(x => x.Year == Year))
                return default;
            Rank rank = new(FantacalcioId.GetRank(GroupName, Year, LeagueName),
                group.Leagues.FirstOrDefault(x => x.Name == LeagueName).Years
                .FirstOrDefault(x => x.Year == Year).Teams
                .Select(x =>
                    new RankedTeam(x.Id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)).ToList()
                );
            int lastDay = 38;
            if (FantacalcioTime.ActualYear == Year)
                lastDay = (await new RealCalendarKey { Year = Year }.InstanceAsync()).LastDay.SerieADay;
            var league = group.Leagues.First(x => x.Name == LeagueName).Years.FirstOrDefault(x => x.Year == Year);
            if (league != default)
            {
                var settings = league.Settings;
                var calendar = await new CalendarKey { GroupName = GroupName, LeagueName = LeagueName, Year = Year }.InstanceAsync();
                if (league.Type == LeagueType.Cup)
                    foreach (var day in calendar.Days.Where(x => x.SerieADay <= 22))
                    {
                        rank.AdjustRound(day);
                    }
                foreach (var day in calendar.Days.Where(x => x.SerieADay <= lastDay))
                    if (league.Type == LeagueType.League || day.SerieADay <= 22)
                    {
                        rank.AddDay(day, settings);
                    }
                return rank;
            }
            else
                return default;
        }
    }
}