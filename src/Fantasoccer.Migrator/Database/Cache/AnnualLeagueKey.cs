using Rystem.Business;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class AnnualLeagueKey : ICacheKey<PlayedAnnualLeague>
    {
        public int Year { get; set; }
        public string GroupName { get; set; }
        public string LeagueName { get; set; }
        public async Task<PlayedAnnualLeague> FetchAsync()
        {
            var group = await new GroupKey { Name = GroupName }.InstanceAsync();
            if (group != default)
            {
                var league = group.Leagues.FirstOrDefault(x => x.Name == LeagueName);
                if (league != default)
                {
                    var year = league.Years.FirstOrDefault(x => x.Year == Year);
                    if (year != default)
                    {
                        var playedAnnualLeague = new PlayedAnnualLeague(year.Id, year.Type, year.Settings, new());
                        foreach (var team in year.Teams)
                        {
                            var teamFromContext = await new TeamContext(team.Id).GetAsync();
                            playedAnnualLeague.Teams.Add(teamFromContext);
                        }
                        return playedAnnualLeague;
                    }
                }
            }
            return default;
        }
    }
}