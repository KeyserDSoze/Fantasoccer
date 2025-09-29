using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class LivePlayer
    {
        [JsonPropertyName("c")]
        public required Player Current { get; set; }
        [JsonPropertyName("v")]
        public Vote? Vote { get; set; }
        [JsonPropertyName("h")]
        public Chance? Chance { get; set; }
        [JsonPropertyName("r")]
        public RealGame? RealGame { get; set; }
        [JsonPropertyName("f")]
        public VoteValue? FinalValue { get; set; }
        [JsonPropertyName("p")]
        public FantaSoccerRole CurrentPosition { get; set; }
    }
}
