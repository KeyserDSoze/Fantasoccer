using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class FinalVotesJob : IBackgroundJob
    {
        private readonly Manager Manager;
        private readonly IRepository<FantacalcioLog, string> _logger;
        public FinalVotesJob(Manager manager, Logger logger)
        {
            Manager = manager;
            _logger = logger;
        }

        public async Task ActionToDoAsync()
        {
            var calendar = await new RealCalendarKey().InstanceAsync().NoContext();
            var liveDay = calendar.LiveDay;
            if (liveDay != default)
            {
                var players = await Manager.Job.SaveFinalVoteAsync(FantacalcioTime.ActualYear, liveDay.SerieADay, true).NoContext();
                await Logger.MessageAsync<FinalVotesJob>($"Players count: {players.Count} for day {liveDay.SerieADay} year {liveDay.Year}").NoContext();
            }
        }

        public Task OnException(Exception exception) 
            => Logger.CatchAsync<FinalVotesJob>(exception);
    }
}