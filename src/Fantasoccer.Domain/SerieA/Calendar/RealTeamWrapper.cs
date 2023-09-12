using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeamWrapper
    {
        [JsonPropertyName("t")]
        public List<RealTeam> Teams { get; set; }
    }
}
