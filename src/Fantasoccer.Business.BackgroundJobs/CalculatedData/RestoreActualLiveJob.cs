using Fantasoccer;
using RepositoryFramework;
using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class RestoreActualLiveJob : IBackgroundJob
    {
        private readonly IRepository<FantasoccerLog, string> _logger;
        private readonly Manager Manager;
        public RestoreActualLiveJob(IRepository<FantasoccerLog, string> logger, IRepository<RealCalendar, int> calendar)
        {
            _logger = logger;
            Manager = manager;
        }

        public async Task ActionToDoAsync()
        {
            var serieA = await Manager.Core.GetSerieACalendarAsync(FantacalcioTime.ActualYear).NoContext();
            foreach (var groupName in await Manager.Group.ListAsync().NoContext())
            {
                List<Task<Live>> tasks = new();
                foreach (var league in await Manager.Group.GetLeaguesAsync(groupName).NoContext())
                {
                    tasks.Add(Manager.Live.GetDayAsync(groupName, league.Name, FantacalcioTime.ActualYear));
                }
                await Task.WhenAll(tasks).NoContext();
                await new LiveKey() { Group = groupName }
                    .RestoreAsync(new ActualLive(tasks.Select(x => x.Result).ToList(), serieA)).NoContext();
            }
            //await Logger.MessageAsync<RestoreActualLiveJob>($"Restore done.").NoContext();
        }

        public Task OnException(Exception exception)
            => Logger.CatchAsync<AllPlayersJob>(exception);
    }
}
