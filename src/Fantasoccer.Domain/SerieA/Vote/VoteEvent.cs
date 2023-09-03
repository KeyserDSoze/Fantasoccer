using System.Collections.Generic;

namespace Fantasoccer
{
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
