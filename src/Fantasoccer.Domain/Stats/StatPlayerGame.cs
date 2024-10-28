using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public sealed class StatPlayerGame
    {
        [JsonPropertyName("d")]
        public int SerieADay { get; set; }
        [JsonPropertyName("v")]
        public float? Vote { get; set; }
        [JsonPropertyName("p")]
        public int Positiveness { get; set; }
    }
}
