using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class GeneralLeagueKey : IDefaultKey
    {
        [JsonPropertyName("g")]
        public required string Group { get; set; }
        [JsonPropertyName("l")]
        public required string League { get; set; }
    }
}
