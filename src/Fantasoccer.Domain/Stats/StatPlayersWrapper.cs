using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public sealed class StatPlayersWrapper
    {
        [JsonPropertyName("p")]
        public required List<StatPlayer> Players { get; set; }
    }
}
