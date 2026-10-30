using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class GeneralLeagueKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("l")]
        public required string League { get; set; }
    }
}
