using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class LeagueKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("l")]
        public required string League { get; set; }
        [JsonPropertyName("y")]
        public required int Year { get; set; }
    }
}
