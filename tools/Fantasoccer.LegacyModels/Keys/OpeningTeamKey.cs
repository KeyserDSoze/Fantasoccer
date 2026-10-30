using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class OpeningTeamKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("l")]
        public required string League { get; set; }
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("b")]
        public required string BasketId { get; set; }
        [JsonPropertyName("e")]
        public required string Email { get; set; }
    }
}
