using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record Vote(Role Role, float Value, bool IsFinal, int Goal, int Penalty,
        int Assist, int StoppedPenalty, int SufferedGoal, int WrongedPenalty,
        int OwnGoal, Behaviour Status, bool HasVote, bool IsOut, bool IsIn)
    {
        public static Vote Default(Role role)
            => new(role, 0, false, 0, 0, 0, 0, 0, 0, 0, Behaviour.Nothing, false, false, false);
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