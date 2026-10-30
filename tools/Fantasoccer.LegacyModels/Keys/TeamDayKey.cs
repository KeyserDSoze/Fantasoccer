using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class TeamDayKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("b")]
        public required string BasketId { get; set; }
        [JsonPropertyName("d")]
        public required int Day { get; set; }
        [JsonPropertyName("e")]
        public required string Email { get; set; }
    }
}
