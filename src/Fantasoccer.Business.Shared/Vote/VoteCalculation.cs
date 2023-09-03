namespace Fantasoccer.Business
{
    internal sealed class VoteCalculation : IVoteCalculation
    {
        public VoteValue FinalValue(VotedRealPlayer player, LeagueSetting leagueSetting)
        {
            var vote = player.Vote;
            if (leagueSetting == null)
                return VoteValue.Empty;
            var voteLeagueSetting = leagueSetting.Votes.ContainsKey(player.Role) ?
                leagueSetting.Votes[player.Role] : leagueSetting.Votes[Role.Undefined];
            var calculatedVote = vote.Value;
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
            var special = vote.HasVote && player.Role == Role.GoalKeeper && vote.SufferedGoal == 0 && leagueSetting.PointForCleanSheet > 0;
            if (special)
                calculatedVote += leagueSetting.PointForCleanSheet;
            return vote.FinalValue = new VoteValue()
            {
                Value = calculatedVote,
                Special = special
            };
        }
    }
}
