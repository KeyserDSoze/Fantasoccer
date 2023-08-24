using System.Timers;

namespace FantacalcioV4.Engine
{
    public class CreateNewYearJob : IBackgroundJob
    {
        private readonly Manager Manager;
        private readonly IRepository<FantacalcioLog, string> _logger;
        public CreateNewYearJob(Manager manager,  IRepository<FantacalcioLog, string> logger)
        {
            Manager = manager;
            _logger = logger;
        }
        public async Task ActionToDoAsync()
        {
            foreach (var group in await Manager.Group.ListAsync().NoContext())
            {
                await Manager.Job.CreateNewYearAndCalendarAsync(group, FantacalcioTime.ActualYear, true).NoContext();
                await Logger.MessageAsync<CreateNewYearJob>($"Create new year for {group}.").NoContext();
            }
        }

        public Task OnException(Exception exception)
             => Logger.CatchAsync<CreateNewYearJob>(exception);
    }
}