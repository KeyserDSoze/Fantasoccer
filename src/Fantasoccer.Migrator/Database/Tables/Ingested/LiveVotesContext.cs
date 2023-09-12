using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public record LiveVotesContext(int Year, int SerieADay)
    {
        public Task SaveAsync(List<Player> players)
            => FantacalcioContext.SaveAsync(FantacalcioId.GetLiveVotes(Year, SerieADay), players);

        public Task<List<Player>> GetAsync()
           => FantacalcioContext.GetAsync<List<Player>>(FantacalcioId.GetLiveVotes(Year, SerieADay));
    }
}