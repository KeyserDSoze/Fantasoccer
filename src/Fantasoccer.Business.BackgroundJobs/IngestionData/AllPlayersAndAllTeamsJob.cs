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
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IAllPlayers _allPlayers;
        private readonly IRepository<Team, TeamKey> _teamRepository;

        public AllPlayersAndAllTeamsJob(IFantasoccerLogger logger,
            IOfficialVote vote,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            IRepository<RealTeamWrapper, int> realTeamsRepository,
            IAllPlayers allPlayers,
            IRepository<Group, string> groupRepository,
            IRepository<Team, TeamKey> teamRepository)
        {
            _logger = logger;
            _vote = vote;
            _realPlayersRepository = realPlayersRepository;
            _realTeamsRepository = realTeamsRepository;
            _allPlayers = allPlayers;
            _groupRepository = groupRepository;
            _teamRepository = teamRepository;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var year = FantacalcioTime.ActualYear;
            var realTeams = await _realTeamsRepository.GetAsync(year);
            var notExistsRealTeams = realTeams == null;
            var notCorrectNumberForRealTeams = realTeams?.Teams?.Count < 20;
            if (notExistsRealTeams || notCorrectNumberForRealTeams)
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
                    if (notCorrectNumberForRealTeams)
                        await _realTeamsRepository.UpdateAsync(year, realTeams);
                    else
                        await _realTeamsRepository.InsertAsync(year, realTeams);
                }
            }
            if (realTeams != null)
            {
                var players = await _allPlayers.GetAsync().NoContext();
                var actualPlayers = await _realPlayersRepository.GetAsync(year).NoContext();
                var playersExist = actualPlayers != null;
                if (!playersExist)
                    actualPlayers = new RealPlayersWrapper { Players = new() };
                foreach (var actualPlayer in actualPlayers!.Players)
                {
                    var realPlayer = players.FirstOrDefault(x => x.Key == actualPlayer.Key);
                    if (realPlayer == null)
                    {
                        actualPlayer.IsActive = false;
                        players.Add(actualPlayer);
                    }
                    else
                    {
                        actualPlayer.RealTeam = realPlayer.RealTeam;
                        actualPlayer.IsActive = true;
                    }
                }
                if (!playersExist)
                    await _realPlayersRepository.InsertAsync(year, new RealPlayersWrapper { Players = players }).NoContext();
                else
                    await _realPlayersRepository.UpdateAsync(year, new RealPlayersWrapper { Players = players }).NoContext();
                foreach (var group in await _groupRepository.ToListAsEntityAsync().NoContext())
                {
                    foreach (var basket in group.Baskets)
                    {
                        if (!basket.Years.Any(x => x.Year == year))
                            continue;
                        foreach (var teamK in basket.Years.First(x => x.Year == year).Teams)
                        {
                            var teamKey = new TeamKey
                            {
                                BasketId = basket.Id,
                                Email = teamK.Owner,
                                Group = group.Id,
                                Year = year
                            };
                            var team = await _teamRepository.GetAsync(teamKey).NoContext();
                            if (team != null)
                            {
                                var hasChanged = false;
                                foreach (var player in team.ActivePlayers)
                                {
                                    var playerFromAllPlayers = players.FirstOrDefault(x => x.Key == player.Key);
                                    if (playerFromAllPlayers != null && (player.RealTeam == null || player.RealTeam.Name != playerFromAllPlayers.RealTeam.Name))
                                    {
                                        player.RealTeam = playerFromAllPlayers.RealTeam;
                                        hasChanged = true;
                                    }
                                }
                                if (hasChanged)
                                {
                                    _ = await _teamRepository.UpdateAsync(teamKey, team);
                                }
                            }
                        }
                    }
                }
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
