using System.Collections.Generic;
using System.Linq;

namespace Fantasoccer
{
    public record Transfer(Team ATeam, Team BTeam, List<RealPlayer> APlayers, List<RealPlayer> BPlayers, int Price, List<TransferAcceptanceVote> Votes)
    {
        public bool IsAllowed 
            => Votes.Where(x => x.Vote == TransferAcceptanceVoteType.Allow).Count() > Votes.Count / 2 + 1;
    }
    public record TransferAcceptanceVote(string Username, TransferAcceptanceVoteType Vote);
    public enum TransferAcceptanceVoteType
    {
        Allow,
        Disallow,
        Abstention
    }
}