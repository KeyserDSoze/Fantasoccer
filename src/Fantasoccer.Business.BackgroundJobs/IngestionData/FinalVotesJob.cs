using System.Timers;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class FinalVotesJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IVotePlayerGenerator _votePlayerGenerator;
        private readonly IStatsPlayerGenerator _statsPlayerGenerator;

        public FinalVotesJob(IFantasoccerLogger logger,
            IRepository<RealCalendar, int> realCalendar,
            IVotePlayerGenerator votePlayerGenerator,
            IStatsPlayerGenerator statsPlayerGenerator)
        {
            _logger = logger;
            _realCalendar = realCalendar;
            _votePlayerGenerator = votePlayerGenerator;
            _statsPlayerGenerator = statsPlayerGenerator;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var calendar = await _realCalendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            var lastDay = calendar?.LastDay;
            if (lastDay != default)
            {
                var checkIfAVoteIsUpdated = await _votePlayerGenerator.GenerateAndSaveAsync(FantacalcioTime.ActualYear, lastDay.SerieADay);
                if (checkIfAVoteIsUpdated > 0)
                {
                    await _statsPlayerGenerator.GenerateAndSaveAsync(lastDay.Year, lastDay.SerieADay);
                }
                await _logger.LogAsync<FinalVotesJob>($"Vote parsed {checkIfAVoteIsUpdated} for year {lastDay.Year}").NoContext();
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<FinalVotesJob>(exception);
    }
}
