using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace FantacalcioV4.Database
{
    public record AllPlayerContext(int Year)
    {
        public Task SaveAsync(List<RealPlayer> players)
           => Task.WhenAll(FantacalcioContext.SaveAsync(FantacalcioId.GetAnnualPlayers(Year), players),
               new AllPlayerKey { Year = Year }.RestoreAsync(players));
        public Task<List<RealPlayer>> GetAsync()
           => FantacalcioContext.GetAsync<List<RealPlayer>>(FantacalcioId.GetAnnualPlayers(Year));
    }
}