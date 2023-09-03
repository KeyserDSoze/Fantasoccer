using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class EnrichedPlayer
    {
        [JsonIgnore]
        public required Player Current { get; set; }
        [JsonIgnore]
        public Vote? Vote { get; set; }
        [JsonIgnore]
        public Chance? Chance { get; set; }
        [JsonIgnore]
        public RealGame? RealGame { get; set; }
        [JsonIgnore]
        public VoteValue FinalValue { get; set; }
        [JsonIgnore]
        public FantaSoccerRole CurrentPosition { get; set; }
    }
}
