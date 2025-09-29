using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class LiveTeam
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("o")]
        public string Owner { get; set; }
        [JsonPropertyName("a")]
        public List<string> AdditionalOwners { get; set; }
        [JsonPropertyName("p")]
        public List<LivePlayer> Players { get; set; }
        [JsonPropertyName("d")]
        public DateTime? LastUpdate { get; set; }
    }

}
