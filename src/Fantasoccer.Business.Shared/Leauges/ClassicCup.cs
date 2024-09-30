using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    internal sealed class ClassicCup : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;

        public ClassicCup(IRankCalculator rankCalculator)
        {
            _rankCalculator = rankCalculator;
        }
        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar)
        {
            var rank = new Rank() { RankedTeams = new(), SerieADay = 0 };
            if (calendar != null)
            {
                foreach (var round in calendar.Rounds)
                {
                    if (round.Key == "Finals")
                        continue;
                    if (!rank.RankedTeams.ContainsKey(round.Key))
                    {
                        rank.RankedTeams.Add(round.Key,
                            round.Value.First().Games.SelectMany(x => new RankedTeam[] { new RankedTeam { Name = x.Home, Owner = x.HomeOwner }, new RankedTeam { Name = x.Away, Owner = x.AwayOwner } }).ToList());
                    }
                    var teams = rank.RankedTeams[round.Key];
                    foreach (var day in round.Value)
                    {
                        foreach (var game in day.Games)
                        {
                            var homeTeam = teams.First(x => x.Owner == game.HomeOwner);
                            var awayTeam = teams.First(x => x.Owner == game.AwayOwner);
                            if (_rankCalculator.AddDay(homeTeam, true, game.Result, leagueSetting) && rank.SerieADay < day.SerieADay)
                                rank.SerieADay = day.SerieADay;
                            if (_rankCalculator.AddDay(awayTeam, false, game.Result, leagueSetting) && rank.SerieADay < day.SerieADay)
                                rank.SerieADay = day.SerieADay;
                        }
                    }
                }
                foreach (var roundKey in calendar.Rounds.Select(x => x.Key))
                {
                    if (roundKey == "Finals")
                        continue;
                    rank.RankedTeams[roundKey] = rank.RankedTeams[roundKey].OrderByDescending(x => x.Point).ThenByDescending(x => x.ValuePoint).ToList();
                }
            }
            return rank;
        }
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator)
            => GetRightFormation(teamCalculator.Enrich(players));
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            if (!players.Any())
                return Array.Empty<EnrichedPlayer>();
            var allPlayers = players.ToList();
            var playersInField = allPlayers.Where(x => x.Current.Position >= FantaSoccerRole.GoalKeeper && x.Current.Position <= FantaSoccerRole.Forward).OrderBy(x => x.Current.Position).ToList();
            var playersAsSubstitute = allPlayers.Where(x => x.Current.Position > FantaSoccerRole.Forward && x.Current.Position <= FantaSoccerRole.SecondBackupForward).OrderBy(x => x.Current.Position).ToList();
            for (var i = 0; i < playersInField.Count; i++)
            {
                if (playersInField[i].Current.Position >= FantaSoccerRole.GoalKeeper
                    && playersInField[i].Current.Position <= FantaSoccerRole.Forward
                    && playersInField[i].Vote?.HasVote != true)
                {
                    for (var j = 0; j < playersAsSubstitute.Count; j++)
                    {
                        if (playersAsSubstitute[j].Current.Position > FantaSoccerRole.Forward
                            && playersAsSubstitute[j].Current.Position <= FantaSoccerRole.SecondBackupForward
                            && playersAsSubstitute[j].CurrentPosition > FantaSoccerRole.Forward
                            && playersAsSubstitute[j].Current.Role == playersInField[i].Current.Role
                            && playersAsSubstitute[j].Vote?.HasVote == true)
                        {
                            var positionSwitcher = playersInField[i].CurrentPosition;
                            playersInField[i].CurrentPosition = playersAsSubstitute[j].CurrentPosition;
                            playersAsSubstitute[j].CurrentPosition = positionSwitcher;
                            break;
                        }
                    }
                }
            }
            playersInField.AddRange(playersAsSubstitute);
            return playersInField;
        }

        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            var allTeams = teams.ToList();
            var randomizedTeams = Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };
            for (var round = 1; round < 5; round++)
            {
                var teamsInRound = randomizedTeams.Skip((round - 1) * 4).Take(4).ToList();
                List<(int Day, int Round, (int TeamA, int TeamB, int Row) Game)> randomizedCalendar = GetCalendar(teamsInRound.Count, round);
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
        private const string Finals = "Finals";
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
    }
}
