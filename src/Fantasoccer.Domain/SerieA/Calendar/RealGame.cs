using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealGame
    {
        [JsonPropertyName("h")]
        public RealTeam Home { get; set; }
        [JsonPropertyName("a")]
        public RealTeam Away { get; set; }
        [JsonPropertyName("d")]
        public DateTime? Date { get; set; }
        [JsonPropertyName("g")]
        public int? HomeGoal { get; set; }
        [JsonPropertyName("y")]
        public int? AwayGoal { get; set; }
        [JsonPropertyName("e")]
        public bool Delayed { get; set; }
        [JsonIgnore]
        public bool IsPlayed => !Delayed && Date.HasValue && FantacalcioTime.ItalyNow > Date.Value;
    }
}
