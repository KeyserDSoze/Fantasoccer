using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager
{
    public sealed class HallOfFameJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<HallOfFame, GeneralLeagueKey> _hallOfFameRepository;
        private readonly IRepository<Team, TeamKey> _teamRepository;
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _officialVoteRepository;

        public HallOfFameJob(IFantasoccerLogger logger,
            IRepository<Group, string> groups,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<Rank, LeagueKey> rankRepository,
            IRepository<HallOfFame, GeneralLeagueKey> hallOfFameRepository,
            IRepository<Team, TeamKey> teamRepository,
            IFactory<ILeagueCalculator> leagueCalculatorFactory,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> voteRepositoryFactory)
        {
            _logger = logger;
            _groups = groups;
            _calendarRepository = calendarRepository;
            _rankRepository = rankRepository;
            _hallOfFameRepository = hallOfFameRepository;
            _teamRepository = teamRepository;
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _officialVoteRepository = voteRepositoryFactory.Create("official")!;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            RecordPlayer? recordPlayer = null;
            PlayerWithMostPointsInYear? playerWithMostPointsInYear = null;
            //todo : implement
            //for (var year = actualYear; year > 0; year--)
            //{
            //    for (var day = 1; day < 39; day++)
            //    {
            //        var votes = await _officialVoteRepository.GetAsync(new YearDayKey
            //        {
            //            Day = day,
            //            Year = year
            //        }).NoContext();
            //        if (votes != null)
            //        {
            //            foreach (var player in votes.Players)
            //            {
            //            }
            //        }
            //    }
            //}
            foreach (var group in await _groups.ToListAsEntityAsync())
            {
                foreach (var league in group.Leagues)
                {
                    var hallOfFame = new HallOfFame()
                    {
                        WinningPlayers = [],
                        WinningTeams = [],
                        Ranks = [],
                        RecordPlayer = recordPlayer,
                        PlayerWithMostPointsInYear = playerWithMostPointsInYear
                    };
                    foreach (var yearlyLeague in league.Years.OrderByDescending(x => x.Year))
                    {
                        var leagueYearlyKey = new LeagueKey { Group = group.Id, League = league.Id, Year = yearlyLeague.Year };
                        var rank = await _rankRepository.GetAsync(leagueYearlyKey).NoContext();
                        var calendar = await _calendarRepository.GetAsync(leagueYearlyKey).NoContext();
                        foreach (var round in rank!.RankedTeams)
                        {
                            foreach (var team in round.Value)
                            {
                                var rankedTeam = hallOfFame.Ranks.FirstOrDefault(x => x.Owner == team.Owner);
                                if (rankedTeam == null)
                                {
                                    rankedTeam = new RankedTeam
                                    {
                                        Owner = team.Owner,
                                        Name = team.Name,
                                    };
                                    hallOfFame.Ranks.Add(rankedTeam);
                                }
                                rankedTeam.Defeats += team.Defeats;
                                rankedTeam.Draws += team.Draws;
                                rankedTeam.Goal += team.Goal;
                                rankedTeam.Money += team.Money;
                                rankedTeam.PlusMoney += team.PlusMoney;
                                rankedTeam.Point += team.Point;
                                rankedTeam.SufferedGoal += team.SufferedGoal;
                                rankedTeam.SufferedValuePoint += team.SufferedValuePoint;
                                rankedTeam.ValuePoint += team.ValuePoint;
                                rankedTeam.Victories += team.Victories;
                            }
                        }
                        foreach (var round in calendar!.Rounds)
                        {
                            foreach (var day in round.Value)
                            {
                                foreach (var game in day.Games)
                                {
                                    if (game.Result == null || !game.Result.HasValue)
                                        continue;
                                    if (hallOfFame.RecordGame == null)
                                        hallOfFame.RecordGame = new RecordGame
                                        {
                                            Game = game,
                                            Year = yearlyLeague.Year
                                        };
                                    else if ((game.Result.Home.Value > hallOfFame.RecordGame.Game.Result.Home.Value && game.Result.Home.Value > hallOfFame.RecordGame.Game.Result.Away.Value) ||
                                        (game.Result.Away.Value > hallOfFame.RecordGame.Game.Result.Home.Value && game.Result.Away.Value > hallOfFame.RecordGame.Game.Result.Away.Value))
                                        hallOfFame.RecordGame = new RecordGame
                                        {
                                            Game = game,
                                            Year = yearlyLeague.Year
                                        };
                                }
                            }
                        }
                        if (yearlyLeague.Year < actualYear || calendar.Rounds.SelectMany(x => x.Value).OrderByDescending(x => x.Number).First().Games.All(x => x.Result != null && x.Result.HasValue))
                        {
                            var leagueCalculator = _leagueCalculatorFactory.Create(league.Type.ToString())!;
                            var winners = leagueCalculator.GetTheYearlyWinners(rank!, calendar!);
                            foreach (var officialWinner in winners)
                            {
                                var winningTeam = group.Baskets.SelectMany(x => x.Years).Where(x => x.Year == yearlyLeague.Year).SelectMany(x => x.Teams).First(x => x.Owner == officialWinner.Owner);
                                var winner = hallOfFame.WinningTeams.FirstOrDefault(x => x.Owner == officialWinner.Owner);
                                if (winner == null)
                                {
                                    winner = new WinningTeam
                                    {
                                        Owner = officialWinner.Owner,
                                        Team = winningTeam.Name,
                                        Wins = new()
                                    };
                                    hallOfFame.WinningTeams.Add(winner);
                                }
                                if (!winner.Wins.ContainsKey(officialWinner.Round))
                                    winner.Wins.Add(officialWinner.Round, new());
                                winner.Wins[officialWinner.Round].Add(yearlyLeague.Year);
                                var basketId = group.Baskets.First(t => t.Years.Any(x => x.Year == yearlyLeague.Year && x.Teams.Any(x => x.Owner == officialWinner.Owner))).Id;
                                var team = await _teamRepository.GetAsync(new TeamKey { Group = group.Id, Email = officialWinner.Owner, Year = yearlyLeague.Year, BasketId = basketId }).NoContext();
                                foreach (var player in team!.ActivePlayers)
                                {
                                    var winningPlayer = hallOfFame.WinningPlayers.FirstOrDefault(x => x.Player.Key == player.Key);
                                    if (winningPlayer == null)
                                    {
                                        winningPlayer = new WinningPlayer
                                        {
                                            Player = player,
                                            Wins = new()
                                        };
                                        hallOfFame.WinningPlayers.Add(winningPlayer);
                                    }
                                    if (!winningPlayer.Wins.ContainsKey(officialWinner.Round))
                                        winningPlayer.Wins.Add(officialWinner.Round, new());
                                    winningPlayer.Wins[officialWinner.Round].Add(yearlyLeague.Year);
                                }
                            }
                        }
                    }
                    var leagueKey = new GeneralLeagueKey { Group = group.Id, League = league.Id };
                    if (await _hallOfFameRepository.ExistAsync(leagueKey).NoContext())
                    {
                        await _hallOfFameRepository.UpdateAsync(leagueKey, hallOfFame).NoContext();
                    }
                    else
                    {
                        await _hallOfFameRepository.InsertAsync(leagueKey, hallOfFame).NoContext();
                    }
                }
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<HallOfFameJob>(exception);
    }
}
