namespace Fantasoccer
{
    public class VotedRealPlayer : RealPlayer
    {
        public Vote Vote { get; set; }
        public void SetVote(VotedRealPlayer player, LeagueSetting settings)
        {
            Vote = player?.Vote ?? Vote.Default(Role);
            Vote.FinalValue(Role, settings);
        }
    }
}
