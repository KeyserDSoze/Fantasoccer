using System.Timers;
using Fantasoccer;
using Fantasoccer.Engine.IngestionData;
using RepositoryFramework;

namespace FantacalcioV4.Engine.IngestionData
{
    public class AllPlayersJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayersRepository;
        private readonly IAllPlayers _allPlayers;

        public AllPlayersJob(IFantasoccerLogger logger,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            IAllPlayers allPlayers)
        {
            _logger = logger;
            _realPlayersRepository = realPlayersRepository;
            _allPlayers = allPlayers;
        }

        public async Task ActionToDoAsync()
        {
            var year = FantacalcioTime.ActualYear;
            var players = await _allPlayers.GetAsync().NoContext();

            var oldPlayers = await _realPlayersRepository.GetAsync(year).NoContext() ?? new RealPlayersWrapper { Players = new List<RealPlayer>() };
            foreach (var oldPlayer in oldPlayers.Players)
            {
                var oldPlayerKey = KeyUtils.GetPlayerKey(oldPlayer.Name);
                if (!players.Any(x => KeyUtils.GetPlayerKey(x.Name) == oldPlayerKey))
                {
                    players.Add(oldPlayer);
                }
                else
                {
                    oldPlayer.IsActive = false;
                }
            }
            await _realPlayersRepository.UpdateAsync(year, oldPlayers).NoContext();
            await _logger.LogAsync<AllPlayersJob>($"Players count: {players.Count} for year {FantacalcioTime.ActualYear}").NoContext();
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<AllPlayersJob>(exception);
    }
}
