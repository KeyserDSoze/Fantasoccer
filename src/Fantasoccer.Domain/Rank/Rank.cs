using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record Rank
    {
        [JsonPropertyName("r")]
        public Dictionary<string, List<RankedTeam>> RankedTeams { get; set; }
    }
}
