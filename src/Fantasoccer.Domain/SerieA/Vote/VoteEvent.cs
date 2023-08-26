using System.Collections.Generic;
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
    public record VoteValue
    {
        public float Value { get; set; } = -1000;
        public bool Special { get; set; }
        public static VoteValue Empty => new();
        public bool IsEmpty => Value == -1000;
        public bool IsZero => Value == 0;
    }
    public record VoteEvent(string Event, string Message)
    {
        public static List<VoteEvent> WhatChange(Vote earlierVote, Vote vote)
        {
            List<VoteEvent> events = new();
            if (vote == default)
                return events;
            if (earlierVote == default)
                earlierVote = Vote.Default(vote.Role);
            if (vote.Assist > earlierVote.Assist)
                events.Add(new VoteEvent("Assist", "{0} ha fatto un assist."));
            else if (earlierVote.Assist > vote.Assist)
                events.Add(new VoteEvent("Assist annullato", "{0} è stato tolto un assist."));
            if (vote.Goal > earlierVote.Goal)
                events.Add(new VoteEvent("Goal", "{0} ha fatto un goal."));
            else if (earlierVote.Goal > vote.Goal)
                events.Add(new VoteEvent("Goal annullato", "{0} è stato tolto un goal."));
            if (vote.OwnGoal > earlierVote.OwnGoal)
                events.Add(new VoteEvent("Autogoal", "{0} ha fatto un autogoal."));
            else if (earlierVote.OwnGoal > vote.OwnGoal)
                events.Add(new VoteEvent("Autogoal annullato", "{0} è stato tolto un autogoal."));
            if (vote.Penalty > earlierVote.Penalty)
                events.Add(new VoteEvent("Goal su rigore", "{0} ha fatto un goal su rigore."));
            else if (earlierVote.Penalty > vote.Penalty)
                events.Add(new VoteEvent("Goal su rigore annulato", "{0} è stato tolto un goal su rigore."));
            if (vote.WrongedPenalty > earlierVote.WrongedPenalty)
                events.Add(new VoteEvent("Rigore sbagliato", "{0} ha sbagliato un rigore."));
            else if (earlierVote.WrongedPenalty > vote.WrongedPenalty)
                events.Add(new VoteEvent("Rigore ripetuto", "{0} è stato fatto ripetere il rigore."));
            if (vote.SufferedGoal > earlierVote.SufferedGoal)
                events.Add(new VoteEvent("Preso goal", "{0} ha preso un goal."));
            else if (earlierVote.SufferedGoal > vote.SufferedGoal)
                events.Add(new VoteEvent("Annulato goal preso", "{0} è stato annullato il goal preso."));
            if (vote.StoppedPenalty > earlierVote.StoppedPenalty)
                events.Add(new VoteEvent("Rigore parato", "{0} ha parato un rigore."));
            else if (earlierVote.StoppedPenalty > vote.StoppedPenalty)
                events.Add(new VoteEvent("Rigore ripetuto", "{0} è stato fatto ripetere il rigore."));
            if (vote.Status > earlierVote.Status)
                events.Add(new VoteEvent("Comportamento", $"{{0}} è stato {(vote.Status == Behaviour.RedCard ? "espulso" : "ammonito")}."));
            else if (earlierVote.Status > vote.Status)
                events.Add(new VoteEvent("Comportamento annullato", $"{{0}} è stata annullata {(earlierVote.Status == Behaviour.RedCard ? "l'espulsione" : "l'ammonizione")}."));
            return events;
        }
    }
}
