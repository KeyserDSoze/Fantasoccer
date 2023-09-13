using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class LeagueKey : IDefaultKey
    {
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("l")]
        public required string League { get; set; }
    }
}
