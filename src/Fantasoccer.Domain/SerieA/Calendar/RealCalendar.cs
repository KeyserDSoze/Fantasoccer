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
        private static readonly TimeSpan s_eightHours = TimeSpan.FromHours(8) + s_twoHours;
        [JsonIgnore]
        public RealDay? LiveDay => Days?.OrderByDescending(day => day.SerieADay).FirstOrDefault(day =>
        {
            var games = day.Games?.Where(x => !x.Delayed && x.Date.HasValue).OrderBy(x => x.Date).ToList();
            if (games == null || games.Count == 0)
                return false;

            return FantacalcioTime.ItalyNow >= games[0].Date!.Value && FantacalcioTime.ItalyNow <= games[^1].Date!.Value + s_eightHours;
        });
        [JsonIgnore]
        public RealDay? LastDay => Days?.OrderByDescending(x => x.SerieADay).FirstOrDefault(day =>
        {
            var lastGame = day.Games?.Where(x => !x.Delayed).OrderBy(x => x.Date).LastOrDefault();
            if (lastGame == null)
                return false;
            return FantacalcioTime.ItalyNow >= lastGame.Date + s_twoHours;
        });
        [JsonIgnore]
        public RealDay? NextDay
        {
            get
            {
                var day = (LiveDay?.SerieADay ?? LastDay?.SerieADay ?? 0) + 1;
                return Days?.FirstOrDefault(x => x.SerieADay == day);
            }
        }

        [JsonIgnore]
        public IEnumerable<RealGame>? LiveGames
            => LiveDay?.Games?.Where(x => !x.Delayed && x.Date.HasValue && FantacalcioTime.ItalyNow >= x.Date.Value && FantacalcioTime.ItalyNow <= x.Date.Value + s_twoHours);
        [JsonIgnore]
        public bool IsLive => LiveGames?.Any() ?? false;
        [JsonIgnore]
        public int LiveSerieADay => LiveDay?.SerieADay ?? 0;
        [JsonIgnore]
        public bool IsDuringSerieADay => LiveSerieADay > 0;
        [JsonIgnore]
        public DateTime? LiveFormationDeadline
            => LiveDay?.Games?
                .Where(x => !x.Delayed && x.Date.HasValue)
                .Select(x => x.Date!.Value)
                .DefaultIfEmpty()
                .Max()
                .AddHours(4);
        [JsonIgnore]
        public bool IsLiveFormationWindowOpen
            => LiveFormationDeadline is DateTime deadline && FantacalcioTime.ItalyNow <= deadline;
    }
}
