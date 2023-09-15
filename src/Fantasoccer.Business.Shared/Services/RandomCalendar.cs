using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    internal sealed class RandomCalendar : IRandomCalendar
    {
        public Calendar GetRandomCalendar(int year, LeagueType type, AnnualLeague league, List<AnnualTeam> teams)
        {
            var randomizedTeams = Randomize(teams);
            var startRound = type == LeagueType.League ? 0 : 1;
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };
            for (var round = startRound; startRound == 0 ? round < 1 : round < 5; round++)
            {
                var teamsInRound = startRound == 0 ? randomizedTeams : randomizedTeams.Skip((round - 1) * 4).Take(4).ToList();
                List<(int Day, int Round, (int TeamA, int TeamB, int Row) Game)> randomizedCalendar = GetCalendar(teamsInRound.Count, round);
                var numberOfDay = 0;
                var homeAndAwayCount = 0;
                do
                {
                    numberOfDay += teamsInRound.Count - 1;
                    homeAndAwayCount++;
                } while (numberOfDay + teamsInRound.Count - 1 <= (startRound == 0 ? 38 : 12));
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
                                var home = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.TeamA] : teamsInRound[game.Game.TeamB];
                                var away = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.TeamB] : teamsInRound[game.Game.TeamA];
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
        private static Dictionary<int, List<int>> GetCalendarBase(int max)
        {
            Dictionary<int, List<int>> array = new();
            for (var i = 0; i < max; i++)
            {
                array.Add(i, new List<int>());
                for (var j = 0; j < max; j++)
                    if (i != j)
                        array[i].Add(j);
            }
            return array;
        }
        private static List<(int Day, int Round, (int A, int B, int Row) Team)> GetCalendar(int teamInGame, int round)
        {
            var dayToCreate = teamInGame - 1;
            var numberOfGameInSingleDay = teamInGame / 2;
restart: var baseCalendar = GetCalendarBase(teamInGame);
            List<(int Day, int Round, (int TeamA, int TeamB, int Row) Game)> games = new();
            for (var i = 1; i <= dayToCreate; i++)
            {
                List<int> exited = new();
                for (var j = 0; j < numberOfGameInSingleDay; j++)
                {
                    int value;
                    do
                    {
                        value = GetRandom(teamInGame);
                    } while (exited.Contains(value));
                    exited.Add(value);
                    int value2;
                    var counter = 0;
                    do
                    {
                        value2 = baseCalendar[value][GetRandom(baseCalendar[value].Count)];
                        counter++;
                        if (counter > 500)
                        {
                            goto restart;
                        }

                    } while (exited.Contains(value2));
                    baseCalendar[value].Remove(value2);
                    baseCalendar[value2].Remove(value);
                    exited.Add(value2);
                    games.Add((i, round, (value, value2, j)));
                }
            }
            return games;
        }
        private static List<AnnualTeam> Randomize(List<AnnualTeam> teams)
        {
            var teamsToAdd = new List<AnnualTeam>();
            foreach (var team in teams)
                teamsToAdd.Add(team);
            List<AnnualTeam> randomizedTeams = new();
            while (teamsToAdd.Count > 0)
            {
                var team = teamsToAdd[GetRandom(teamsToAdd.Count)];
                randomizedTeams.Add(team);
                teamsToAdd.Remove(team);
            }
            return randomizedTeams;
        }
        private static int GetRandom(int max)
        {
            var value = RandomNumberGenerator.GetInt32(0, max);
            if (max == value)
                return max - 1;
            return value;
        }
        public Calendar AddDaysInLeague(int year, LeagueType type, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
        {
            List<Team> nextTeams = new();
            var toPlayGames = calendar.Rounds.SelectMany(x => x.Value).SelectMany(x => x.Games).Count(x => x.Result == null || !x.Result.HasValue);
            var serieADay = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.SerieADay);
            var counter = calendar.Rounds.SelectMany(x => x.Value).Max(x => x.Number);
            if (toPlayGames == 0)
            {
                if (!calendar.Rounds.ContainsKey("finals"))
                {
                    foreach (var teamInGroup in rank.RankedTeams)
                        nextTeams.AddRange(teamInGroup.Value.Take(2).Select(x => new Team { Owner = x.Owner, Name = x.Name }));
                    calendar.Rounds.Add("finals", new());
                }
                else if (calendar.Rounds["finals"].Count > 1)
                {
                    var round = calendar.Rounds["finals"];
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
                    var finals = calendar.Rounds["Finals"];
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
                            var away = adder == 2 ? nextTeams[nextTeams.Count - 1 - i] : nextTeams[i];
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
    }
}
