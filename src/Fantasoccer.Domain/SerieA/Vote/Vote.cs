using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Vote
    {
        [JsonPropertyName("r")]
        public Role Role { get; set; }
        [JsonPropertyName("v")]
        public float Value { get; set; }
        [JsonPropertyName("i")]
        public bool IsFinal { get; set; }
        [JsonPropertyName("g")]
        public int Goal { get; set; }
        [JsonPropertyName("p")]
        public int Penalty { get; set; }
        [JsonPropertyName("a")]
        public int Assist { get; set; }
        [JsonPropertyName("s")]
        public int StoppedPenalty { get; set; }
        [JsonPropertyName("d")]
        public int SufferedGoal { get; set; }
        [JsonPropertyName("w")]
        public int WrongedPenalty { get; set; }
        [JsonPropertyName("o")]
        public int OwnGoal { get; set; }
        [JsonPropertyName("t")]
        public Behaviour Status { get; set; }
        [JsonPropertyName("h")]
        public bool HasVote { get; set; }
        [JsonPropertyName("u")]
        public bool IsOut { get; set; }
        [JsonPropertyName("n")]
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
    }
}
