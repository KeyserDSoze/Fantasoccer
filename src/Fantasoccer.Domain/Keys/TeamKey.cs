using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class TeamKey : IDefaultKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("b")]
        public required string BasketId { get; set; }
        [JsonPropertyName("e")]
        public required string Email { get; set; }
    }
}
