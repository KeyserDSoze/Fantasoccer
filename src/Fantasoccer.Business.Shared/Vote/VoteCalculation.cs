namespace Fantasoccer.Business
{
    internal sealed class VoteCalculation : IVoteCalculation
    {
        public Vote? GetVote(Player player, VotedRealPlayerWrapper? official, VotedRealPlayerWrapper? live, LeagueSetting? settings)
        {
            var votePlayer = official?.Players.FirstOrDefault(x => x.Key == player.Key);
            if (votePlayer == null)
                votePlayer = live?.Players.FirstOrDefault(x => x.Key == player.Key);
            if (votePlayer != null && settings != null)
                votePlayer.Vote.FinalValue = FinalValue(votePlayer, settings);
            return votePlayer?.Vote;
        }
        public Point CalculatePoint(Team team, VotedRealPlayerWrapper? official, VotedRealPlayerWrapper? live, LeagueSetting leagueSetting)
        {
            if (team.Players.Count == 0)
                return new Point(0F, false, false);
            var point = 0F;
            var defensiveBonus = true;
            var defensorCount = 0;
            foreach (var player in GetRightFormation().InField)
            {
                if (player.Vote != default && player.Vote.HasVote)
                {
                    point += player.Vote.FinalValue(player.Role, leagueSetting).Value;
                    if (player.Role == Role.Defensor)
                        defensorCount++;
                }
                if (player.Role == Role.Defensor && (player.Vote == default || !player.Vote.HasVote || player.Vote.Value < 6))
                    defensiveBonus = false;
            }
            if (defensiveBonus)
            {
                if (defensorCount == 3)
                    point += leagueSetting.PointForStrongDefense;
                else if (defensorCount == 4)
                    point += leagueSetting.PointForStrongDefense4;
                else if (defensorCount == 5)
                    point += leagueSetting.PointForStrongDefense5;
            }
            return new Point(point, defensiveBonus,
                leagueSetting.PointForOwnGoal > 0 && point < leagueSetting.PointForFirstGoal - leagueSetting.PointForOwnGoal);
        }
        public (List<Player> InField, List<Player> Substitutes) GetRightFormation(Team team, LeagueSetting setting)
        {
            if (team.Players.Count == 0)
                return (new List<Player>(), new List<Player>());
            var allPlayers = team.ActivePlayers.OrderBy(x => x.Status).ToList();
            var playersInField = team.ActivePlayers.Where(x => x.Position >= FantaSoccerRole.GoalKeeper && x.Position <= FantaSoccerRole.Forward).OrderBy(x => x.Status).ToList();
            var playersAsSubstitute = team.ActivePlayers.Where(x => x.Position > FantaSoccerRole.Forward && x.Position <= FantaSoccerRole.SecondBackupForward).OrderBy(x => x.Status).ToList();
            for (var i = 0; i < playersInField.Count; i++)
            {
                if (playersInField[i].Position >= FantaSoccerRole.GoalKeeper &&
                    playersInField[i].Position <= FantaSoccerRole.Forward)
                {
                    var vote = this.GetVote(playersInField[i], official, live, setting);
                    if (playersInField[i].Vote != default && !playersInField[i].Vote.HasVote)
                    {
                        for (var j = 0; j < playersAsSubstitute.Count; j++)
                        {
                            if (playersAsSubstitute[j].Role == playersInField[i].Role && playersAsSubstitute[j].Vote.HasVote)
                            {
                                Position positionSubstitute = new(playersAsSubstitute[j].Position.X, playersAsSubstitute[j].Position.Y);
                                Position positionInField = new(playersInField[i].Position.X, playersInField[i].Position.Y);
                                playersInField[i].PositionBeforeChange = playersInField[i].Position;
                                playersAsSubstitute[j].PositionBeforeChange = playersAsSubstitute[j].Position;
                                var switcher = playersInField[i];
                                playersInField[i] = playersAsSubstitute[j];
                                playersAsSubstitute[j] = switcher;
                                playersInField[i].Position = positionInField;
                                playersAsSubstitute[j].Position = positionSubstitute;
                                break;
                            }
                        }
                    }
                }
            }
            return (playersInField, playersAsSubstitute);
        }
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
