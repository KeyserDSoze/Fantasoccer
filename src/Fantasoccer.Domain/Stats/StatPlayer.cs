using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public class StatPlayer : RealPlayer
    {
        [JsonIgnore]
        public float Average => WithVote > 0 ? Summatory / WithVote : 0;
        [JsonIgnore]
        public float FantaAverage => WithVote > 0 ? FantaSummatory / WithVote : 0;
        [JsonPropertyName("z")]
        public float Summatory { get; set; }
        [JsonPropertyName("f")]
        public float FantaSummatory { get; set; }
        [JsonPropertyName("v")]
        public int WithVote { get; set; }
        [JsonPropertyName("q")]
        public int WithoutVote { get; set; }
        [JsonPropertyName("s")]
        public int WithSpecial { get; set; }
        [JsonPropertyName("g")]
        public int Goal { get; set; }
        [JsonPropertyName("p")]
        public int Penalty { get; set; }
        [JsonPropertyName("u")]
        public int Assist { get; set; }
        [JsonPropertyName("j")]
        public int StoppedPenalty { get; set; }
        [JsonPropertyName("m")]
        public int SufferedGoal { get; set; }
        [JsonPropertyName("w")]
        public int WrongedPenalty { get; set; }
        [JsonPropertyName("o")]
        public int OwnGoal { get; set; }
        [JsonPropertyName("y")]
        public int YellowCard { get; set; }
        [JsonPropertyName("d")]
        public int RedCard { get; set; }
        [JsonPropertyName("e")]
        public int IsEnough { get; set; }
        [JsonPropertyName("c")]
        public int ManOfTheMatch { get; set; }
        [JsonPropertyName("ij")]
        public int Injured { get; set; }
    }
}
