using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public record FinalVotesContext(int Year, int SerieADay)
    {
        public async Task SaveAsync(List<Player> players, bool forceRefresh)
        {
            await FantacalcioContext.SaveAsync(FantacalcioId.GetFinalVotes(Year, SerieADay), players);
            if (players.Count > 0 && forceRefresh)
                await new AllVoteKey() { Year = Year }.RestoreAsync();
        }

        public Task<List<Player>> GetAsync()
           => FantacalcioContext.GetAsync<List<Player>>(FantacalcioId.GetFinalVotes(Year, SerieADay));
    }
}