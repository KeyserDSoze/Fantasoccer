using FantacalcioV4.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.ApiManager
{
    public class CoreApi
    {
        public async Task<List<RealPlayer>> GetAllPlayersAsync(int year)
        {
            var players = await new AllPlayerKey { Year = year }.InstanceAsync();
            return players.ToList();
        }

        public Task<List<Player>> GetVotesAsync(int year, int serieADay)
            => new FinalVotesContext(year, serieADay).GetAsync();
        public Task<List<SummedVotesPlayer>> GetAllVotedPlayersAsync(int year)
            => new AllVoteKey() { Year = year }.InstanceAsync();
        public Task<List<Player>> GetLiveVotesAsync(int year, int serieADay)
            => new LiveVotesContext(year, serieADay).GetAsync();
        public Task<List<Player>> GetChancesAsync()
            => new PlayerOddsContext().GetAsync();
        public Task<RealCalendar> GetSerieACalendarAsync(int year)
            => new RealCalendarKey() { Year = year }.InstanceAsync();
        public Task<IEnumerable<StoredSerieATeam>> GetCurrentSerieATeamAsync()
            => AllTeamKey.Active.InstanceAsync();
    }
}
