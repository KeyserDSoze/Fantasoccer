using System;
using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealGame
    {
        public string Home { get; set; }
        public string Away { get; set; }
        public string AbbreviatedHome { get; set; }
        public string AbbreviatedAway { get; set; }
        public DateTime? Date { get; set; }
        public int? HomeGoal { get; set; }
        public int? AwayGoal { get; set; }
        public bool Delayed { get; set; }
        [JsonIgnore]
        public bool IsPlayed => !Date.HasValue || FantacalcioTime.ItalyNow > Date.Value;
    }
}
