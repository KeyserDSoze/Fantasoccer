using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class PlayerOddsContext
    {
        private const string Odds = "Odds";
        public Task SaveAsync(List<Player> players)
          => FantacalcioContext.SaveAsync(Odds, players);
        public Task<List<Player>> GetAsync()
            => FantacalcioContext.GetAsync<List<Player>>(Odds);
    }
}
