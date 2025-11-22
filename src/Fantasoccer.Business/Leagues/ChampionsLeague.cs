using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class ChampionsLeague : ILeagueCalculator
    {
        private readonly IRandomCalendar _randomCalendar;
        private readonly ILeagueCalculator? _defaultLeagueCalculator;
        public ChampionsLeague(IFactory<ILeagueCalculator> factory, IRandomCalendar randomCalendar)
        {
            _randomCalendar = randomCalendar;
            _defaultLeagueCalculator = factory.Create(string.Empty);
        }
        private const string ChampionsFinals = "Finals";
        private const string EuropaLeagueFinals = "Europa League";
        //todo: super cup to add
        private const string SuperCup = "Supercoppa";
        private static readonly List<string> s_avoidCalculationRounds = new() { ChampionsFinals, EuropaLeagueFinals };
        private static readonly int s_multiplier = 1;
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
        {
            var toPlayGames = calendar.Rounds.SelectMany(x => x.Value).SelectMany(x => x.Games).Count(x => x.Result == null || !x.Result.HasValue);
            var serieADay = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.SerieADay);
            var numberOfDaysUntilNow = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.Number);
            if (toPlayGames == 0)
            {
                List<Team> winners = [];
                for (var i = 0; i < s_avoidCalculationRounds.Count; i++)
                {
                    var theWinner = Calculate(i, s_avoidCalculationRounds[i], serieADay, calendar, rank, numberOfDaysUntilNow);
                    if (theWinner != null)
                    {
                        winners.Add(theWinner);
                    }
                }
                if (winners.Count == 2)
                {
                    var day = new Day
                    {
                        Number = 38,
                        Games = new(),
                        SerieADay = 38
                    };
                    calendar.Rounds.Add(SuperCup, []);
                    calendar.Rounds[SuperCup].Add(day);
                    day.Games.Add(new CalendarGame
                    {
                        Id = Guid.NewGuid().ToString(),
                        Number = 1,
                        Home = winners[0].Name,
                        HomeOwner = winners[0].Owner,
                        Away = winners[1].Name,
                        AwayOwner = winners[1].Owner,
                    });
                }
                static Team? Calculate(int index, string roundName, int serieADay, Calendar calendar, Rank rank, int numberOfDaysUntilNow)
                {
                    List<Team> nextTeams = new();
                    if (!calendar.Rounds.ContainsKey(roundName))
                    {
                        foreach (var teamInGroup in rank.RankedTeams)
                            nextTeams.AddRange(teamInGroup.Value.Skip(index * 8).Take(8).Select(x => new Team { Owner = x.Owner, Name = x.Name }));
                        calendar.Rounds.Add(roundName, new());
                    }
                    else if (!calendar.Rounds.ContainsKey(SuperCup) && calendar.Rounds[roundName].Any(x => x.Games.Count == 1 && x.Games.All(t => t.Result != null)))
                    {
                        var theFinal = calendar.Rounds[roundName].Last().Games.First();
                        if (theFinal.Result!.HomeGoal > theFinal.Result.AwayGoal
                            || theFinal.Result.Home.Value > theFinal.Result.Away.Value)
                            return new Team { Owner = theFinal.HomeOwner, Name = theFinal.Home };
                        else if (theFinal.Result.AwayGoal > theFinal.Result.HomeGoal
                            || theFinal.Result.Away.Value > theFinal.Result.Home.Value)
                            return new Team { Owner = theFinal.AwayOwner, Name = theFinal.Away };
                    }
                    else if (calendar.Rounds[roundName].Count > 1)
                    {
                        var round = calendar.Rounds[roundName];
                        var first = round.First(x => x.SerieADay == serieADay - s_multiplier);
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
                        var finals = calendar.Rounds[roundName];
                        for (var adder = 1; nextTeams.Count == 2 ? adder <= 1 : adder <= 2; adder++)
                        {
                            var nextSerieADay = serieADay + adder * s_multiplier;
                            var day = new Day
                            {
                                Number = numberOfDaysUntilNow + adder,
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
                    return null;
                }
            }
            return calendar;
        }
        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            var key = ((char)(64)).ToString();
            var allTeams = teams.ToList();
            var randomizedTeams = _randomCalendar.Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };
            var randomizedCalendar = _randomCalendar.GetCalendar(randomizedTeams.Count, 1);
            var numberOfDays = randomizedTeams.Count - 1;
            var homeAndAwayCounts = 1;
            var actualDay = 1;
            for (var homeAwayRound = 0; homeAwayRound < homeAndAwayCounts; homeAwayRound++)
            {
                if (!calendar.Rounds.ContainsKey(key))
                    calendar.Rounds.Add(key, []);
                var singleRound = calendar.Rounds[key];
                foreach (var rounds in randomizedCalendar.GroupBy(x => x.Day))
                {
                    foreach (var games in rounds.GroupBy(x => x.Round))
                    {
                        var serieADay = actualDay + league.Settings.DelayedDay;
                        var isCancelled = actualDay <= league.Settings.CancelledDay;
                        var day = new Day { Games = [], SerieADay = serieADay, Number = actualDay };
                        singleRound.Add(day);
                        var gameCount = 1;
                        foreach (var game in games)
                        {
                            var home = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.ATeam] : randomizedTeams[game.Game.BTeam];
                            var away = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.BTeam] : randomizedTeams[game.Game.ATeam];
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
            return calendar;

        }

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
        {
            return _defaultLeagueCalculator!.GetRightFormation(players, teamCalculator);
        }

        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            return _defaultLeagueCalculator!.GetRightFormation(players);
        }
        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
        {
            var winners = new List<LeagueWinner>();
            if (calendar != null)
            {
                if (calendar.Rounds.ContainsKey(ChampionsFinals) && calendar.Rounds[ChampionsFinals].Any())
                {
                    var champions = calendar.Rounds[ChampionsFinals].OrderByDescending(x => x.Number).Last().Games.First();
                    var championsWinner = GetWinner(champions);
                    if (championsWinner != null)
                        winners.Add(new LeagueWinner
                        {
                            Owner = championsWinner,
                            Round = ChampionsFinals
                        });
                }
                if (calendar.Rounds.ContainsKey(EuropaLeagueFinals) && calendar.Rounds[EuropaLeagueFinals].Any())
                {
                    var europaLeague = calendar.Rounds[EuropaLeagueFinals].OrderByDescending(x => x.Number).Last().Games.First();
                    var europaWinner = GetWinner(europaLeague);
                    if (europaWinner != null)
                        winners.Add(new LeagueWinner
                        {
                            Owner = europaWinner,
                            Round = EuropaLeagueFinals
                        });
                }
            }
            return winners;

            string? GetWinner(CalendarGame game)
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
