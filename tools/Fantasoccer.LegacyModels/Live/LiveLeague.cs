using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class LiveLeague
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("l")]
        public string Name { get; set; }
        [JsonPropertyName("d")]
        public Dictionary<string, Day> Rounds { get; set; }
        [JsonPropertyName("r")]
        public Rank Rank { get; set; }
    }
}
