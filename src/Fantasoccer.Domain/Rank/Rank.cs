using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class Rank
    {
        [JsonPropertyName("r")]
        public Dictionary<string, List<RankedTeam>> RankedTeams { get; set; }
    }
}
