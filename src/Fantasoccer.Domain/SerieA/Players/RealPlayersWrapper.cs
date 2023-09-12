using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealPlayersWrapper
    {
        [JsonPropertyName("p")]
        public List<RealPlayer> Players { get; set; }
    }
}
