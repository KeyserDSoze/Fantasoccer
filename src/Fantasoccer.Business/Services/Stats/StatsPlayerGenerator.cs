using Fantasoccer.Domain;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class StatsPlayerGenerator : IStatsPlayerGenerator
    {
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _officialVoteRepository;
        private readonly IRepository<StatPlayersWrapper, int> _statsRepository;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayersRepository;
        private readonly ITeamCalculator _teamCalculator;

        public StatsPlayerGenerator(IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<StatPlayersWrapper, int> statsRepository,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            ITeamCalculator teamCalculator)
        {
            _officialVoteRepository = factory.Create("official");
            _statsRepository = statsRepository;
            _realPlayersRepository = realPlayersRepository;
            _teamCalculator = teamCalculator;
            _teamCalculator.SetLeagueSettings(LeagueSetting.Default);
        }
        public async ValueTask GenerateAndSaveAsync(int year, int untilThisDay)
        {
            var wrapper = new StatPlayersWrapper() { Players = new() };
            var realPlayersWrapper = await _realPlayersRepository.GetAsync(year);
            foreach (var realPlayer in realPlayersWrapper.Players)
            {
                var statsPlayer = new StatPlayer()
                {
                    Name = realPlayer?.Name,
                    RealTeam = realPlayer?.RealTeam,
                    IsActive = realPlayer?.IsActive ?? false,
                    Role = realPlayer?.Role ?? Role.Undefined
                };
                wrapper.Players.Add(statsPlayer);
            }
            for (var serieADay = untilThisDay; serieADay > 0; serieADay--)
            {
                var key = new YearDayKey
                {
                    Day = serieADay,
                    Year = year,
                };
                var votes = await _officialVoteRepository.GetAsync(key);
                if (votes != null)
                {
                    foreach (var player in votes.Players)
                    {
                        var playerKey = player.Key;
                        var statsPlayer = wrapper.Players.FirstOrDefault(x => x.Key == playerKey);
                        if (statsPlayer != null)
                        {
                            if (player.Vote != null)
                            {
                                if (player.Vote.HasVote)
                                {
                                    statsPlayer.WithVote++;
                                    statsPlayer.Penalty += player.Vote.Penalty;
                                    statsPlayer.SufferedGoal += player.Vote.SufferedGoal;
                                    statsPlayer.StoppedPenalty += player.Vote.StoppedPenalty;
                                    statsPlayer.Assist += player.Vote.Assist;
                                    statsPlayer.OwnGoal += player.Vote.OwnGoal;
                                    statsPlayer.Goal += player.Vote.Goal;
                                    if (player.Vote.Status == Behaviour.RedCard)
                                        statsPlayer.RedCard++;
                                    if (player.Vote.Status == Behaviour.YellowCard)
                                        statsPlayer.YellowCard += player.Vote.Penalty;
                                    if (player.Vote.Value >= 6)
                                        statsPlayer.IsEnough++;
                                    statsPlayer.Summatory += player.Vote.Value;
                                    var value = _teamCalculator.FinalValue(_teamCalculator.Enrich(new Player() { Role = player.Role }, null, null, player.Vote));
                                    statsPlayer.FantaSummatory += value.Value;
                                    if (value.Special)
                                        statsPlayer.WithSpecial++;
                                }
                                else
                                {
                                    statsPlayer.WithoutVote++;
                                }
                            }
                        }
                    }
                }
            }
            if (await _statsRepository.ExistAsync(year))
            {
                await _statsRepository.UpdateAsync(year, wrapper);
            }
            else
            {
                await _statsRepository.InsertAsync(year, wrapper);
            }
        }
    }
}
