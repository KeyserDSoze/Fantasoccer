using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public sealed class StatPlayer : RealPlayer
    {
        [JsonIgnore]
        public float Average => WithVote > 0 ? Summatory / WithVote : 0;
        [JsonIgnore]
        public float FantaAverage => WithVote > 0 ? FantaSummatory / WithVote : 0;
        public float Summatory { get; set; }
        public float FantaSummatory { get; set; }
        public int WithVote { get; set; }
        public int WithoutVote { get; set; }
        public int WithSpecial { get; set; }
        public int Goal { get; set; }
        public int Penalty { get; set; }
        public int Assist { get; set; }
        public int StoppedPenalty { get; set; }
        public int SufferedGoal { get; set; }
        public int WrongedPenalty { get; set; }
        public int OwnGoal { get; set; }
        public int YellowCard { get; set; }
        public int RedCard { get; set; }
        public int IsEnough { get; set; }
    }
}
