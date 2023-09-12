using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class VotedRealPlayerWrapper
    {
        [JsonPropertyName("p")]
        public List<VotedRealPlayer> Players { get; set; }
    }
}
