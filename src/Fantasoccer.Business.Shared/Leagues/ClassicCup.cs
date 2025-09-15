using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class ClassicCup : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;
        private readonly IRandomCalendar _randomCalendar;
        private readonly ILeagueCalculator? _defaultLeagueCalculator;
        public ClassicCup(IRankCalculator rankCalculator, IFactory<ILeagueCalculator> factory, IRandomCalendar randomCalendar)
        {
            _rankCalculator = rankCalculator;
            _randomCalendar = randomCalendar;
            _defaultLeagueCalculator = factory.Create(string.Empty);
        }
        private const string Finals = "Finals";
        private static readonly List<string> s_avoidCalculationRounds = new() { Finals };
        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, List<string>? avoidCalculationRounds = null)
        {
            if (avoidCalculationRounds != null)
            {
                avoidCalculationRounds.AddRange(s_avoidCalculationRounds);
                return _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, avoidCalculationRounds);
            }
            else
                return _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, s_avoidCalculationRounds);
        }
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator)
            => _defaultLeagueCalculator!.GetRightFormation(players, teamCalculator);
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            return _defaultLeagueCalculator!.GetRightFormation(players);
        }
        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            var allTeams = teams.ToList();
            var randomizedTeams = _randomCalendar.Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };
            for (var round = 1; round < 5; round++)
            {
                var teamsInRound = randomizedTeams.Skip((round - 1) * 4).Take(4).ToList();
                var randomizedCalendar = _randomCalendar.GetCalendar(teamsInRound.Count, round);
                var numberOfDay = 0;
                var homeAndAwayCount = 0;
                do
                {
                    numberOfDay += teamsInRound.Count - 1;
                    homeAndAwayCount++;
                } while (numberOfDay + teamsInRound.Count - 1 <= 12);
                var actualDay = 1;
                for (var homeAwayRound = 0; homeAwayRound < homeAndAwayCount; homeAwayRound++)
                {
                    foreach (var gameByDay in randomizedCalendar.GroupBy(x => x.Round))
                    {
                        var key = ((char)(64 + gameByDay.Key)).ToString();
                        if (!calendar.Rounds.ContainsKey(key))
                            calendar.Rounds.Add(key, new());
                        var singleRound = calendar.Rounds[key];
                        foreach (var games in gameByDay.GroupBy(x => x.Day))
                        {
                            var serieADay = actualDay + league.Settings.DelayedDay;
                            var isCancelled = actualDay <= league.Settings.CancelledDay;
                            var day = new Day { Games = new(), SerieADay = serieADay, Number = actualDay };
                            singleRound.Add(day);
                            var gameCount = 1;
                            foreach (var game in games)
                            {
                                var home = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.ATeam] : teamsInRound[game.Game.BTeam];
                                var away = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.BTeam] : teamsInRound[game.Game.ATeam];
                                day.Games.Add(new CalendarGame
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Number = gameCount,
                                    Home = home.Name,
                                    HomeOwner = home.Owner,
                                    Away = away.Name,
                                    AwayOwner = away.Owner,
                                    Result = isCancelled ? new GameResult { IsCancelled = true } : null
                                });
                                gameCount++;
                            }
                            actualDay++;
                        }
                    }
                }
            }
            return calendar;
        }
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
        {
            List<Team> nextTeams = new();
            var toPlayGames = calendar.Rounds.SelectMany(x => x.Value).SelectMany(x => x.Games).Count(x => x.Result == null || !x.Result.HasValue);
            var serieADay = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.SerieADay);
            var counter = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.Number);
            if (toPlayGames == 0)
            {
                if (!calendar.Rounds.ContainsKey(Finals))
                {
                    foreach (var teamInGroup in rank.RankedTeams)
                        nextTeams.AddRange(teamInGroup.Value.Take(2).Select(x => new Team { Owner = x.Owner, Name = x.Name }));
                    calendar.Rounds.Add(Finals, new());
                }
                else if (calendar.Rounds[Finals].Count > 1)
                {
                    var round = calendar.Rounds[Finals];
                    var first = round.First(x => x.SerieADay == serieADay - 3);
                    var second = round.First(x => x.SerieADay == serieADay);
                    foreach (var game in first.Games)
                    {
                        var secondGame = second.Games.First(x => x.HomeOwner == game.AwayOwner);
                        var goalA = game.Result!.HomeGoal + secondGame.Result!.AwayGoal;
                        var goalB = game.Result.AwayGoal + secondGame.Result.HomeGoal;
                        if (goalA == goalB)
                        {
                            goalA = game.Result.HomeGoal + 2 * secondGame.Result.AwayGoal;
                            goalB = 2 * game.Result.AwayGoal + secondGame.Result.HomeGoal;
                        }
                        if (goalA > goalB)
                            nextTeams.Add(new Team { Owner = game.HomeOwner, Name = game.Home });
                        else if (goalB > goalA)
                            nextTeams.Add(new Team { Owner = game.AwayOwner, Name = game.Away });
                        else
                        {
                            double pointA = game.Result.Home.Value + secondGame.Result.Away.Value;
                            double pointB = game.Result.Away.Value + secondGame.Result.Home.Value;
                            if (pointA > pointB)
                                nextTeams.Add(new Team { Owner = game.HomeOwner, Name = game.Home });
                            else if (pointB > pointA)
                                nextTeams.Add(new Team { Owner = game.AwayOwner, Name = game.Away });
                            else
                            {
                                var valueA = rank.RankedTeams.SelectMany(x => x.Value).First(x => x.Owner == game.HomeOwner).ValuePoint;
                                var valueB = rank.RankedTeams.SelectMany(x => x.Value).First(x => x.Owner == game.AwayOwner).ValuePoint;
                                if (valueA > valueB)
                                    nextTeams.Add(new Team { Owner = game.HomeOwner, Name = game.Home });
                                else if (pointB > pointA)
                                    nextTeams.Add(new Team { Owner = game.AwayOwner, Name = game.Away });
                                else if (RandomNumberGenerator.GetInt32(100) < 50)
                                    nextTeams.Add(new Team { Owner = game.HomeOwner, Name = game.Home });
                                else
                                    nextTeams.Add(new Team { Owner = game.AwayOwner, Name = game.Away });
                            }
                        }
                    }
                }
                if (nextTeams.Count > 0)
                {
                    var finals = calendar.Rounds[Finals];
                    for (var adder = 1; nextTeams.Count == 2 ? adder <= 1 : adder <= 2; adder++)
                    {
                        var nextSerieADay = serieADay + adder * 3;
                        var day = new Day
                        {
                            Number = counter + adder,
                            Games = new(),
                            SerieADay = nextSerieADay
                        };
                        finals.Add(day);
                        var gameCounter = 1;
                        for (var i = 0; i < nextTeams.Count / 2; i++)
                        {
                            var home = adder == 1 ? nextTeams[i] : nextTeams[nextTeams.Count - 1 - i];
                            var away = adder == 1 ? nextTeams[nextTeams.Count - 1 - i] : nextTeams[i];
                            day.Games.Add(new CalendarGame
                            {
                                Id = Guid.NewGuid().ToString(),
                                Number = gameCounter,
                                Home = home.Name,
                                HomeOwner = home.Owner,
                                Away = away.Name,
                                AwayOwner = away.Owner,
                            });
                            gameCounter++;
                        }
                    }
                }
            }
            return calendar;
        }
        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
        {
            var winners = new List<LeagueWinner>();
            if (calendar != null)
            {
                var champions = calendar.Rounds[Finals].OrderByDescending(x => x.Number).LastOrDefault()?.Games.FirstOrDefault();
                if (champions != null)
                {
                    var championsWinner = GetWinner(champions);
                    if (championsWinner != null)
                        winners.Add(new LeagueWinner
                        {
                            Owner = championsWinner,
                            Round = Finals
                        });
                }
            }
            return winners;

            static string? GetWinner(CalendarGame game)
            {
                if (!game.HasNoResult)
                {
                    if (game.Result.HomeGoal > game.Result.AwayGoal)
                        return game.HomeOwner;
                    else if (game.Result.AwayGoal > game.Result.HomeGoal)
                        return game.AwayOwner;
                    else if (game.Result.Home.Value > game.Result.Away.Value)
                        return game.HomeOwner;
                    else if (game.Result.Away.Value > game.Result.Home.Value)
                        return game.AwayOwner;
                    else if (RandomNumberGenerator.GetInt32(100) < 50)
                        return game.HomeOwner;
                    else
                        return game.AwayOwner;
                }
                return default;
            }
        }
    }
}
