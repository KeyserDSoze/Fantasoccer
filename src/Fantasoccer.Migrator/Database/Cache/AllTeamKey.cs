using Rystem.Business;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class AllTeamKey : ICacheKey<IEnumerable<StoredSerieATeam>>
    {
        public static AllTeamKey All { get; } = new AllTeamKey() { A = "A" };
        public static AllTeamKey Active { get; } = new AllTeamKey() { A = "B" };
        public string A { get; set; }
        public async Task<IEnumerable<StoredSerieATeam>> FetchAsync()
        {
            if (A == "A")
                return (await new StoredSerieATeam().GetAsync()).ToList();
            else
                return (await new StoredSerieATeam().GetAsync(x => x.IsActive == true)).ToList();
        }
    }
}