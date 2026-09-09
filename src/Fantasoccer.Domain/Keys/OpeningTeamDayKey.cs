using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class OpeningTeamDayKey : IDefaultKey
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
        [JsonPropertyName("d")]
        public required int Day { get; set; }
    }
}
