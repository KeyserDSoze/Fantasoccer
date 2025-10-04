using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RecordPlayer
    {
        [JsonPropertyName("p")]
        public Player Player { get; set; }
        [JsonPropertyName("pts")]
        public float Points { get; set; }
    }
}
