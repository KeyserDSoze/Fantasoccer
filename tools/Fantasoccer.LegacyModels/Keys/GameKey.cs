using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class GameKey
    {
        [JsonPropertyName("g")]
        public string Group { get; set; }
        [JsonPropertyName("l")]
        public string League { get; set; }
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
        [JsonPropertyName("i")]
        public string GameId { get; set; }
    }
}
