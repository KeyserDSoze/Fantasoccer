using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    internal sealed class ClassicLeague : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;

        public ClassicLeague(IRankCalculator rankCalculator)
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
            var key = ((char)(64)).ToString();
            var allTeams = teams.ToList();
            var randomizedTeams = Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };

            List<(int Day, (int TeamA, int TeamB, int Row) Game)> randomizedCalendar = GetCalendar(randomizedTeams.Count);
            var numberOfDays = randomizedTeams.Count - 1;
            var homeAndAwayCounts = (38 - league.Settings.DelayedDay) / numberOfDays;
            var actualDay = 1;
            for (var homeAwayRound = 0; homeAwayRound < homeAndAwayCounts; homeAwayRound++)
            {
                if (!calendar.Rounds.ContainsKey(key))
                    calendar.Rounds.Add(key, []);
                var singleRound = calendar.Rounds[key];
                foreach (var games in randomizedCalendar.GroupBy(x => x.Day))
                {
                    var serieADay = actualDay + league.Settings.DelayedDay;
                    var isCancelled = actualDay <= league.Settings.CancelledDay;
                    var day = new Day { Games = [], SerieADay = serieADay, Number = actualDay };
                    singleRound.Add(day);
                    var gameCount = 1;
                    foreach (var game in games)
                    {
                        var home = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.TeamA] : randomizedTeams[game.Game.TeamB];
                        var away = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.TeamB] : randomizedTeams[game.Game.TeamA];
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
        private static List<(int Day, (int A, int B, int Row) Team)> GetCalendar(int teamInGame)
        {
            var dayToCreate = teamInGame - 1;
            var numberOfGameInSingleDay = teamInGame / 2;
restart: var baseCalendar = GetCalendarBase(teamInGame);
            List<(int Day, (int TeamA, int TeamB, int Row) Game)> games = new();
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
                    games.Add((i, (value, value2, j)));
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
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
            => calendar;
    }
}
