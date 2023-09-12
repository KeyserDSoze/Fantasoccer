using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class RealCalendar
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public List<RealDay> Days { get; set; }
        private static readonly TimeSpan s_twoHours = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15);
        private static readonly TimeSpan s_fifteenHours = TimeSpan.FromHours(8) + s_twoHours;
        [JsonIgnore]
        public RealDay? LiveDay => Days.FirstOrDefault(day =>
        {
            var firstGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).First();
            var lastGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).Last();
            return FantacalcioTime.ItalyNow >= firstGame.Date && FantacalcioTime.ItalyNow <= lastGame.Date + s_fifteenHours;
        });
        [JsonIgnore]
        public RealDay? LastDay => Days.OrderByDescending(x => x.SerieADay).FirstOrDefault(day =>
        {
            var lastGame = day.Games.Where(x => !x.Delayed).OrderBy(x => x.Date).Last();
            return FantacalcioTime.ItalyNow >= lastGame.Date + s_twoHours;
        });
        [JsonIgnore]
        public RealDay? NextDay
        {
            get
            {
                var day = (LiveDay?.SerieADay ?? LastDay?.SerieADay ?? 0) + 1;
                return Days.FirstOrDefault(x => x.SerieADay == day);
            }
        }

        [JsonIgnore]
        public IEnumerable<RealGame>? LiveGames
            => LiveDay?.Games.Where(x => !x.Delayed && FantacalcioTime.ItalyNow >= x.Date && FantacalcioTime.ItalyNow <= x.Date + s_twoHours);
        [JsonIgnore]
        public bool IsLive => LiveGames?.Any() ?? false;
        [JsonIgnore]
        public int LiveSerieADay => LiveDay?.SerieADay ?? 0;
        [JsonIgnore]
        public bool IsDuringSerieADay => LiveSerieADay > 0;
    }
}
