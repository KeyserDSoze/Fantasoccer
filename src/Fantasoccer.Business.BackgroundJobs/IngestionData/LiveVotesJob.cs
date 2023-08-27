using System.Timers;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class LiveVotesJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ILiveVote _liveVote;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _repository;
        public LiveVotesJob(IFantasoccerLogger logger,
            ILiveVote liveVote,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory)
        {
            _logger = logger;
            _liveVote = liveVote;
            _repository = factory.Create("live");
        }
        public async Task ActionToDoAsync()
        {
            var players = await _liveVote.GetAsync();
            if (players.Any())
            {
                var key = new YearDayKey { Year = FantacalcioTime.ActualYear, Day = _liveVote.Day };
                var parsedPlayers = await _repository.GetAsync(key).NoContext() ?? new VotedRealPlayerWrapper() { Players = new() };
                foreach (var player in players)
                {
                    var currentPlayer = parsedPlayers.Players.FirstOrDefault(x => x.Key == player.Key);
                    if (currentPlayer != null)
                    {
                        currentPlayer.Vote = player.Vote;
                    }
                    else
                    {
                        parsedPlayers.Players.Add(player);
                    }
                }
                await _repository.UpdateAsync(key, parsedPlayers).NoContext();
            }
            await _logger.LogAsync<LiveVotesJob>($"Players {players.Count} parsati.").NoContext();
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<LiveVotesJob>(exception);
    }
}
