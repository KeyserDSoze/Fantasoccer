namespace FantacalcioV4
{
    public static class VoteExtensions
    {
        public static VoteValue FinalValue(this Vote vote, Role role, LeagueSetting leagueSetting)
        {
            if (leagueSetting == null)
                return VoteValue.Empty;
            VoteLeagueSetting voteLeagueSetting = leagueSetting.Votes.ContainsKey(role) ?
                leagueSetting.Votes[role] : leagueSetting.Votes[Role.Undefined];
            float calculatedVote = vote.Value;
            calculatedVote += vote.Goal * voteLeagueSetting.Goal;
            calculatedVote += vote.Penalty * voteLeagueSetting.Penalty;
            calculatedVote += vote.SufferedGoal * voteLeagueSetting.SufferedGoal;
            calculatedVote += vote.StoppedPenalty * voteLeagueSetting.StoppedPenalty;
            calculatedVote += vote.WrongedPenalty * voteLeagueSetting.WrongedPenalty;
            calculatedVote += vote.OwnGoal * voteLeagueSetting.OwnGoal;
            calculatedVote += vote.Assist * voteLeagueSetting.Assist;
            if (vote.Status == Behaviour.YellowCard)
                calculatedVote -= voteLeagueSetting.YellowCard;
            else if (vote.Status == Behaviour.RedCard)
                calculatedVote -= voteLeagueSetting.RedCard;
            bool special = false;
            if (special = (vote.HasVote && role == Role.GoalKeeper && vote.SufferedGoal == 0 && leagueSetting.PointForCleanSheet > 0))
                calculatedVote += leagueSetting.PointForCleanSheet;
            return vote.FinalValue = new VoteValue(calculatedVote, special);
        }
    }
}