namespace Fantasoccer
{
    public record VoteLeagueSetting
    {
        public float Goal { get; set; }
        public float Penalty { get; set; }
        public float SufferedGoal { get; set; }
        public float StoppedPenalty { get; set; }
        public float WrongedPenalty { get; set; }
        public float OwnGoal { get; set; }
        public float Assist { get; set; }
        public float YellowCard { get; set; }
        public float RedCard { get; set; }
    }
}
