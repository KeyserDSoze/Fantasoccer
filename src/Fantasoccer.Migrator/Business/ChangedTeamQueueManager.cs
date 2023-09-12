using FantacalcioV4.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.ApiManager
{
    public static class ChangedTeamQueueManager
    {
        public static async Task ExecuteAsync(IEnumerable<ChangedTeam> changedTeams)
        {
            if (changedTeams.Any())
            {
                var calendar = await new RealCalendarKey().InstanceAsync();
                DateTime maxTime = calendar.LiveDay?.Games.OrderBy(x => x.Date).First().Date ?? DateTime.MaxValue;
                int serieADay = calendar.LiveDay?.SerieADay ?? calendar.LastDay.SerieADay + 1;

                await UpdateTeamsAsync(changedTeams
                    .Where(x => x.WhenItHappened <= maxTime)
                    .GroupBy(x => x.Team.Id)
                    .Select(x => x.OrderByDescending(x => x.WhenItHappened).First()), serieADay);

                await UpdateTeamsAsync(changedTeams
                    .Where(x => x.WhenItHappened > maxTime)
                    .GroupBy(x => x.Team.Id)
                    .Select(x => x.OrderByDescending(x => x.WhenItHappened).First()), serieADay + 1);

                static async Task UpdateTeamsAsync(IEnumerable<ChangedTeam> teams, int serieADay)
                {
                    if (serieADay > 38)
                        return;
                    foreach (ChangedTeam changedTeam in teams)
                    {
                        var context = new TeamContext(changedTeam.Team.Id, serieADay);
                        var team = await context.GetAsync();
                        if (team == default)
                            team = new Team(changedTeam.Team.Id, changedTeam.Team.Players);
                        else
                        {
                            team.Players.RemoveAll(x => true);
                            team.Players.AddRange(changedTeam.Team.Players);
                        }
                        await context.SaveAsync(team);
                    }
                }
            }
        }
    }
}
