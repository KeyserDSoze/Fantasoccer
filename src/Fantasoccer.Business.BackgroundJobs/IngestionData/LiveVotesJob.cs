using System.Timers;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class LiveVotesJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ILiveVote _liveVote;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _repository;
        public LiveVotesJob(IFantasoccerLogger logger,
            ILiveVote liveVote,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<RealCalendar, int> serieACalendar)
        {
            _logger = logger;
            _liveVote = liveVote;
            _serieACalendar = serieACalendar;
            _repository = factory.Create("live");
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear, cancellationToken).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay;
                if (liveDay != default && realCalendar.IsLive)
                {
                    var players = await _liveVote.GetAsync(actualYear, liveDay.SerieADay);
                    if (players.Any())
                    {
                        var key = new YearDayKey { Year = FantacalcioTime.ActualYear, Day = liveDay.SerieADay };
                        var storedPlayers = await _repository.GetAsync(key, cancellationToken).NoContext();
                        var parsedPlayers = storedPlayers ?? new VotedRealPlayerWrapper() { Players = new() };
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
                        if (storedPlayers == null)
                            await _repository.InsertAsync(key, parsedPlayers, cancellationToken).NoContext();
                        else
                            await _repository.UpdateAsync(key, parsedPlayers, cancellationToken).NoContext();
                    }
                    await _logger.LogAsync<LiveVotesJob>($"Players {players.Count} parsati.").NoContext();
                }
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<LiveVotesJob>(exception);
    }
}
