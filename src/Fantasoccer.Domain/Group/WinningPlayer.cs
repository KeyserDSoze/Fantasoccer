using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class WinningPlayer
    {
        [JsonPropertyName("p")]
        public Player Player { get; set; }
        [JsonPropertyName("w")]
        public Dictionary<string, List<int>> Wins { get; set; }
    }
}
