using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealRank
    {
        [JsonPropertyName("r")]
        public List<RealRankedTeam> Teams { get; set; }
    }
}
