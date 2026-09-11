using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public sealed class PlayerDescription
    {
        [JsonPropertyName("top")]
        public string? Top { get; set; }

        [JsonPropertyName("down")]
        public string? Down { get; set; }
    }
}
