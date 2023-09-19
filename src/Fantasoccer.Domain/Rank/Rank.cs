using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class Rank
    {
        [JsonPropertyName("d")]
        public int SerieADay { get; set; }
        [JsonPropertyName("r")]
        public Dictionary<string, List<RankedTeam>> RankedTeams { get; set; }
    }
}
