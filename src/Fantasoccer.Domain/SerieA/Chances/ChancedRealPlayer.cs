using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class ChancedRealPlayer : RealPlayer
    {
        [JsonPropertyName("c")]
        public Chance Chance { get; set; }
    }
}
