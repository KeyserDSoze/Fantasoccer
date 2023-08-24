using System;
using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed record RealGame(string Home, string Away, string AbbreviatedHome, string AbbreviatedAway)
    {
        public DateTime? Date { get; set; }
        public int? HomeGoal { get; set; }
        public int? AwayGoal { get; set; }
        public bool Delayed { get; set; }
        [JsonIgnore]
        public bool IsPlayed => !Date.HasValue || FantacalcioTime.ItalyNow > Date.Value;
    }
}