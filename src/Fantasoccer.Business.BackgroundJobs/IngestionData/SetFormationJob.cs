using Fantasoccer;
using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class SetFormationJob : IBackgroundJob
    {
        private readonly IRepository<FantasoccerLog, string> _logger;
        private readonly Manager Manager;
        public SetFormationJob( IRepository<FantasoccerLog, string> logger, Manager manager)
        {
            _logger = logger;
            Manager = manager;
        }

        public async Task ActionToDoAsync()
        {
            foreach (var groupName in await Manager.Group.ListAsync().NoContext())
            {
                var leagues = await Manager.Group.GetLeaguesAsync(groupName).NoContext();
                var teams = leagues.SelectMany(x => x.Years.FirstOrDefault(x => x.Year == FantacalcioTime.ActualYear).Teams)
                    .GroupBy(x => x.Id);
                foreach (var team in teams)
                {
                    var actualTeam = await Manager.Group.GetTeamAsync(groupName, team.First().Username, FantacalcioTime.ActualYear).NoContext();
                    await Manager.Live.SaveTeamAsync(actualTeam, groupName, team.First().Username, FantacalcioTime.ActualYear, true, FantacalcioTime.ItalyNow.AddMinutes(-5)).NoContext();
                }
            }
            await Logger.MessageAsync<SetFormationJob>($"Formations set.").NoContext();
        }

        public Task OnException(Exception exception)
            => Logger.CatchAsync<AllPlayersJob>(exception);
    }
}