namespace Fantasoccer.Business
{
    public interface IVoteCalculation
    {
        VoteValue FinalValue(VotedRealPlayer player, LeagueSetting leagueSetting);

    }
}
