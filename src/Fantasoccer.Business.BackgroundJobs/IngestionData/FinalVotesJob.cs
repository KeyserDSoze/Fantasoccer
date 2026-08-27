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
            var actualYear = FantacalcioTime.ActualYear;
            var calendar = await _realCalendar.GetAsync(actualYear, cancellationToken).NoContext();
            var lastDay = calendar?.LiveDay ?? calendar?.LastDay;
            if (lastDay != default)
            {
                var checkIfAVoteIsUpdated = await _votePlayerGenerator.GenerateAndSaveAsync(actualYear, lastDay.SerieADay, false, lastDay.SerieADay);
                if (checkIfAVoteIsUpdated)
                {
                    await _statsPlayerGenerator.GenerateAndSaveAsync(actualYear, lastDay.SerieADay);
                }
                await _logger.LogAsync<FinalVotesJob>($"Vote parsed {checkIfAVoteIsUpdated} for year {actualYear}").NoContext();
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<FinalVotesJob>(exception);
    }
}
