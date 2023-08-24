using Rystem;
using System.Collections.Generic;
using System.Linq;

namespace FantacalcioV4
{
    public static class AnnualLeagueExtensions
    {
        public static Calendar GetRandomCalendar(this AnnualLeague league)
        {
            var randomizedTeams = Randomize(league.Teams);
            int startRound = league.Type == LeagueType.League ? 0 : 1;
            var calendar = new Calendar(FantacalcioId.GetCalendar(league.GroupName, league.Year, league.Name), new());
            for (int round = startRound; startRound == 0 ? round < 1 : round < 5; round++)
            {
                var teamsInRound = startRound == 0 ? randomizedTeams : randomizedTeams.Skip((round - 1) * 4).Take(4).ToList();
                List<(int Day, int Round, (int TeamA, int TeamB, int Row) Game)> randomizedCalendar = GetCalendar(teamsInRound.Count, round);
                int numberOfDay = 0;
                int homeAndAwayCount = 0;
                do
                {
                    numberOfDay += teamsInRound.Count - 1;
                    homeAndAwayCount++;
                } while (numberOfDay + teamsInRound.Count - 1 <= (startRound == 0 ? 38 : 12));
                for (int homeAwayRound = 0; homeAwayRound < homeAndAwayCount; homeAwayRound++)
                {
                    foreach (var gameByDay in randomizedCalendar.GroupBy(x => x.Day))
                    {
                        int serieADay = calendar.Days.Where(x => x.Games.Any(t => t.Key == round)).LastOrDefault()?.SerieADay + 1 ?? league.Settings.DelayedDay + 1;
                        var daily = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay);
                        if (daily == default)
                        {
                            daily = new Daily(serieADay, new());
                            calendar.Days.Add(daily);
                        }
                        foreach (var gameByRound in gameByDay.OrderBy(x => x.Round).GroupBy(x => x.Round))
                        {
                            if (!daily.Games.ContainsKey(gameByRound.Key))
                                daily.Games.Add(gameByRound.Key, new());
                            foreach (var game in gameByRound)
                            {
                                var home = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.TeamA] : teamsInRound[game.Game.TeamB];
                                var away = homeAwayRound % 2 == 0 ? teamsInRound[game.Game.TeamB] : teamsInRound[game.Game.TeamA];
                                daily.Games[game.Round].Add(new PlayedGame(FantacalcioId.GetGameId(league.GroupName, league.Year, league.Name,
                                    serieADay, home.Username, away.Username)));
                            }
                        }
                    }
                }
            }
            return calendar;
        }
        private static Dictionary<int, List<int>> GetCalendarBase(int max)
        {
            Dictionary<int, List<int>> array = new();
            for (int i = 0; i < max; i++)
            {
                array.Add(i, new List<int>());
                for (int j = 0; j < max; j++)
                    if (i != j)
                        array[i].Add(j);
            }
            return array;
        }
        private static List<(int Day, int Round, (int A, int B, int Row) Team)> GetCalendar(int teamInGame, int round)
        {
            int dayToCreate = teamInGame - 1;
            int numberOfGameInSingleDay = teamInGame / 2;
            restart: Dictionary<int, List<int>> baseCalendar = GetCalendarBase(teamInGame);
            List<(int Day, int Round, (int TeamA, int TeamB, int Row) Game)> games = new();
            for (int i = 1; i <= dayToCreate; i++)
            {
                List<int> exited = new();
                for (int j = 0; j < numberOfGameInSingleDay; j++)
                {
                    int value = -1;
                    do
                    {
                        value = GetRandom(teamInGame);
                    } while (exited.Contains(value));
                    exited.Add(value);
                    int value2 = -1;
                    int counter = 0;
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
        private static List<Team> Randomize(List<Team> teams)
        {
            var teamsToAdd = new List<Team>();
            foreach (var team in teams)
                teamsToAdd.Add(team);
            List<Team> randomizedTeams = new();
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
            int value = Alea.GetNumber(max);
            if (max == value)
                return max - 1;
            return value;
        }
    }
}