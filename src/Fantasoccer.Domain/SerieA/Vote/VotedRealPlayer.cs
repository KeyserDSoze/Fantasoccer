using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class VotedRealPlayer : RealPlayer
    {
        [JsonPropertyName("v")]
        public Vote Vote { get; set; }
    }
}
