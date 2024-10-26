using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealRankedTeam
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("p")]
        public float Point { get; set; }
        [JsonPropertyName("v")]
        public int Victories { get; set; }
        [JsonPropertyName("d")]
        public int Draws { get; set; }
        [JsonPropertyName("e")]
        public int Defeats { get; set; }
        [JsonPropertyName("g")]
        public int Goal { get; set; }
        [JsonPropertyName("s")]
        public int SufferedGoal { get; set; }
        [JsonPropertyName("k")]
        public int Played { get; set; }
    }
}
