using System.Timers;
using Fantasoccer.Business.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class AllPlayersAndAllTeamsJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IOfficialVote _vote;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayersRepository;
        private readonly IRepository<RealTeamWrapper, int> _realTeamsRepository;
        private readonly IAllPlayers _allPlayers;

        public AllPlayersAndAllTeamsJob(IFantasoccerLogger logger,
            IOfficialVote vote,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            IRepository<RealTeamWrapper, int> realTeamsRepository,
            IAllPlayers allPlayers)
        {
            _logger = logger;
            _vote = vote;
            _realPlayersRepository = realPlayersRepository;
            _realTeamsRepository = realTeamsRepository;
            _allPlayers = allPlayers;
        }

        public async Task ActionToDoAsync()
        {
            var year = FantacalcioTime.ActualYear;
            var realTeams = await _realTeamsRepository.GetAsync(year);
            if (realTeams == null)
            {
                var officialVotes = await _vote.GetAsync(year, 1);
                if (officialVotes != null)
                {
                    var teams = officialVotes.GroupBy(x => x.RealTeam.Name).Select(x => x.Key).ToList();
                    realTeams = new RealTeamWrapper { Teams = new() };
                    foreach (var team in teams)
                    {
                        realTeams.Teams.Add(new RealTeam
                        {
                            Name = team
                        });
                    }
                    await _realTeamsRepository.InsertAsync(year, realTeams);
                }
            }
            if (realTeams != null)
            {
                var players = await _allPlayers.GetAsync().NoContext();

                var actualPlayers = await _realPlayersRepository.GetAsync(year).NoContext();
                var playersExist = actualPlayers != null;
                if (!playersExist)
                    actualPlayers = new RealPlayersWrapper { Players = new List<RealPlayer>() };
                foreach (var actualPlayer in actualPlayers.Players)
                {
                    if (!players.Any(x => x.Key == actualPlayer.Key))
                    {
                        actualPlayer.IsActive = false;
                        players.Add(actualPlayer);
                    }
                }
                if (!playersExist)
                    await _realPlayersRepository.InsertAsync(year, new RealPlayersWrapper { Players = players }).NoContext();
                else
                    await _realPlayersRepository.UpdateAsync(year, new RealPlayersWrapper { Players = players }).NoContext();
                await _logger.LogAsync<AllPlayersAndAllTeamsJob>($"Players count: {players.Count} for year {FantacalcioTime.ActualYear}").NoContext();
            }
            else
            {
                await _logger.LogAsync<AllPlayersAndAllTeamsJob>($"Serie not started because official votes doesn't exist for year {FantacalcioTime.ActualYear}").NoContext();
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<AllPlayersAndAllTeamsJob>(exception);
    }
}
