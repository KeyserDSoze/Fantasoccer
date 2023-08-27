using System.Timers;
using Fantasoccer;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace FantacalcioV4.Engine.IngestionData
{
    public class FinalVotesJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IOfficialVote _finalVote;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _repository;

        public FinalVotesJob(IFantasoccerLogger logger, IOfficialVote finalVote,
            IRepository<RealCalendar, int> realCalendar,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory)
        {
            _logger = logger;
            _finalVote = finalVote;
            _realCalendar = realCalendar;
            _repository = factory.Create("official");
        }

        public async Task ActionToDoAsync()
        {
            var calendar = await _realCalendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            var liveDay = calendar?.LiveDay;
            if (liveDay != default)
            {
                var players = await _finalVote.GetAsync(FantacalcioTime.ActualYear, liveDay.SerieADay).NoContext();
                await _repository.UpdateAsync(new YearDayKey
                {
                    Day = liveDay.SerieADay,
                    Year = liveDay.Year,
                }, new VotedRealPlayerWrapper
                {
                    Players = players,
                });
                await _logger.LogAsync<FinalVotesJob>($"Players count: {players.Count} for day {liveDay.SerieADay} year {liveDay.Year}").NoContext();
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<FinalVotesJob>(exception);
    }
}
