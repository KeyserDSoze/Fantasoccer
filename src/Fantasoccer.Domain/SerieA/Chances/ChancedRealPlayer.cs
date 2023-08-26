namespace Fantasoccer
{
    public class ChancedRealPlayer : RealPlayer
    {
        public Chance Chance { get; set; }
        public Vote Vote { get; set; }
        public void SetVote(ChancedRealPlayer player, LeagueSetting settings)
        {
            Vote = player?.Vote ?? Vote.Default(Role);
            Vote.FinalValue(Role, settings);
        }
    }
}
