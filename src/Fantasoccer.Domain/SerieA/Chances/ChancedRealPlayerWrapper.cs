using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class ChancedRealPlayerWrapper
    {
        [JsonPropertyName("p")]
        public List<ChancedRealPlayer> Players { get; set; }
    }
}
