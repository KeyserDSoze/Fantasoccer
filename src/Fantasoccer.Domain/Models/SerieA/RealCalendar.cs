using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record RealCalendar(int Year, List<RealDay> Days)
    {
        private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15);
        private static readonly TimeSpan FifteenHours = TimeSpan.FromHours(8) + TwoHours;
        [JsonIgnore]
        public RealDay LiveDay => Days.FirstOrDefault(day =>
        {
            var firstGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).First();
            var lastGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).Last();
            return FantacalcioTime.ItalyNow >= firstGame.Date && FantacalcioTime.ItalyNow <= lastGame.Date + FifteenHours;
        });
        [JsonIgnore]
        public RealDay LastDay => Days.OrderByDescending(x => x.SerieADay).FirstOrDefault(day =>
        {
            var lastGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).Last();
            return FantacalcioTime.ItalyNow >= lastGame.Date + TwoHours;
        });
        [JsonIgnore]
        public RealDay NextDay
            => Days.FirstOrDefault(x => x.SerieADay == (LastDay.SerieADay >= 38 ? LastDay.SerieADay : (LastDay.SerieADay <= 0 ? 1 : LastDay.SerieADay + 1)));
        [JsonIgnore]
        public IEnumerable<RealGame> LiveGames
            => LiveDay?.Games.Where(x => !x.Delayed && FantacalcioTime.ItalyNow >= x.Date && FantacalcioTime.ItalyNow <= x.Date + TwoHours);
        [JsonIgnore]
        public bool IsLive => LiveGames?.Any() ?? false;
        [JsonIgnore]
        public int LiveSerieADay => LiveDay?.SerieADay ?? 0;
        [JsonIgnore]
        public bool IsDuringSerieADay => LiveSerieADay > 0;
    }
}