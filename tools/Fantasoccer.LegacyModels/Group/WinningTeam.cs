using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class WinningTeam
    {
        [JsonPropertyName("o")]
        public string Owner { get; set; }
        [JsonPropertyName("t")]
        public string Team { get; set; }
        [JsonPropertyName("w")]
        public Dictionary<string, List<int>> Wins { get; set; }
    }
}
