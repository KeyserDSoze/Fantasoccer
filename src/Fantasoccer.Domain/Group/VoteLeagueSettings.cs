using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record VoteLeagueSetting
    {
        [JsonPropertyName("g")]
        public float Goal { get; set; }
        [JsonPropertyName("p")]
        public float Penalty { get; set; }
        [JsonPropertyName("s")]
        public float SufferedGoal { get; set; }
        [JsonPropertyName("d")]
        public float StoppedPenalty { get; set; }
        [JsonPropertyName("w")]
        public float WrongedPenalty { get; set; }
        [JsonPropertyName("o")]
        public float OwnGoal { get; set; }
        [JsonPropertyName("a")]
        public float Assist { get; set; }
        [JsonPropertyName("y")]
        public float YellowCard { get; set; }
        [JsonPropertyName("r")]
        public float RedCard { get; set; }
        [JsonPropertyName("j")]
        public int Injury { get; set; }
        [JsonPropertyName("m")]
        public int ManOfTheMatch { get; set; }
    }
}
