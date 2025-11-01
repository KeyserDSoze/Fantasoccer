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
            _teamCalculator.SetLeagueSettings(LeagueType.League, LeagueSetting.Default);
        }
        public async ValueTask GenerateAndSaveAsync(int year, int untilThisDay)
        {
            var wrapper = new StatPlayersWrapper() { Players = new() };
            var realPlayersWrapper = await _realPlayersRepository.GetAsync(year);
            foreach (var realPlayer in realPlayersWrapper!.Players)
            {
                var statsPlayer = new StatPlayer()
                {
                    Name = realPlayer?.Name!,
                    RealTeam = realPlayer?.RealTeam!,
                    IsActive = realPlayer?.IsActive ?? false,
                    Role = realPlayer?.Role ?? Role.Undefined,
                    AllGames = []
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
                foreach (var player in wrapper.Players)
                {
                    var playerKey = player.Key;
                    var votePlayer = votes?.Players?.FirstOrDefault(x => x.Key == playerKey);
                    if (votePlayer == null)
                    {
                        player.NoPlayed++;
                        player.AllGames!.Add(new StatPlayerGame
                        {
                            Positiveness = -2,
                            SerieADay = serieADay,
                            Vote = null
                        });
                    }
                    else
                    {
                        var statPlayerGame = new StatPlayerGame
                        {
                            Positiveness = 0,
                            SerieADay = serieADay,
                            Vote = votePlayer.Vote?.HasVote == true ? votePlayer.Vote.Value : null
                        };
                        player.AllGames!.Add(statPlayerGame);
                        if (votePlayer.Vote != null)
                        {
                            if (votePlayer.Vote.HasVote)
                            {
                                player.WithVote++;
                                player.Penalty += votePlayer.Vote.Penalty;
                                if (votePlayer.Vote.Penalty > 0)
                                    statPlayerGame.Positiveness += 2;
                                player.SufferedGoal += votePlayer.Vote.SufferedGoal;
                                if (votePlayer.Vote.SufferedGoal > 0)
                                    statPlayerGame.Positiveness -= 1;
                                player.StoppedPenalty += votePlayer.Vote.StoppedPenalty;
                                if (votePlayer.Vote.StoppedPenalty > 0)
                                    statPlayerGame.Positiveness += 2;
                                player.Assist += votePlayer.Vote.Assist;
                                if (votePlayer.Vote.Assist > 0)
                                    statPlayerGame.Positiveness += 1;
                                player.OwnGoal += votePlayer.Vote.OwnGoal;
                                if (votePlayer.Vote.OwnGoal > 0)
                                    statPlayerGame.Positiveness -= 2;
                                player.Goal += votePlayer.Vote.Goal;
                                if (votePlayer.Vote.Goal > 0)
                                    statPlayerGame.Positiveness += 2;
                                player.WrongedPenalty += votePlayer.Vote.WrongedPenalty;
                                if (votePlayer.Vote.WrongedPenalty > 0)
                                    statPlayerGame.Positiveness -= 2;
                                if (votePlayer.Vote.Status == Behaviour.RedCard)
                                {
                                    player.RedCard++;
                                    statPlayerGame.Positiveness -= 2;
                                }
                                if (votePlayer.Vote.Status == Behaviour.YellowCard)
                                {
                                    player.YellowCard++;
                                    statPlayerGame.Positiveness -= 1;
                                }
                                if (votePlayer.Vote.Value >= 6)
                                {
                                    player.IsEnough++;
                                    statPlayerGame.Positiveness += 1;
                                }
                                if (votePlayer.Vote.ManOfTheMatch)
                                {
                                    player.ManOfTheMatch++;
                                    statPlayerGame.Positiveness += 1;
                                }
                                if (votePlayer.Vote.Injured)
                                {
                                    player.Injured++;
                                    statPlayerGame.Positiveness -= 2;
                                }
                                player.Summatory += votePlayer.Vote.Value;
                                var value = _teamCalculator.FinalValue(_teamCalculator.ForceEnrich(new Player() { Role = player.Role }, votePlayer.Vote, null, null));
                                player.FantaSummatory += value.Value;
                                if (value.Special)
                                    player.WithSpecial++;
                            }
                            else
                            {
                                player.WithoutVote++;
                                statPlayerGame.Positiveness -= 2;
                            }
                        }
                        else
                        {
                            player.NoPlayed++;
                            statPlayerGame.Positiveness -= 2;
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
