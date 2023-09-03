using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Vote
    {
        public Role Role { get; set; }
        public float Value { get; set; }
        public bool IsFinal { get; set; }
        public int Goal { get; set; }
        public int Penalty { get; set; }
        public int Assist { get; set; }
        public int StoppedPenalty { get; set; }
        public int SufferedGoal { get; set; }
        public int WrongedPenalty { get; set; }
        public int OwnGoal { get; set; }
        public Behaviour Status { get; set; }
        public bool HasVote { get; set; }
        public bool IsOut { get; set; }
        public bool IsIn { get; set; }
        public static Vote Default(Role role)
            => new() { Role = role };
        [JsonIgnore]
        public bool HasDoneSomething
            => HasBonus || (Value > 0 && this.Role == Role.GoalKeeper);
        [JsonIgnore]
        public bool HasBonus => HasPositiveBonus || HasNegativeBonus;
        [JsonIgnore]
        public bool HasPositiveBonus
            => Goal > 0 || Penalty > 0 || Assist > 0 || StoppedPenalty > 0;
        [JsonIgnore]
        public bool HasNegativeBonus
            => OwnGoal > 0 || Status > 0 || WrongedPenalty > 0 || SufferedGoal > 0;
        [JsonIgnore]
        public VoteValue FinalValue { get; set; }
    }
}
