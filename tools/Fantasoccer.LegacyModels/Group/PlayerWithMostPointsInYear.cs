using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class PlayerWithMostPointsInYear
    {
        [JsonPropertyName("p")]
        public Player Player { get; set; }
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("pts")]
        public float Points { get; set; }
    }
}
