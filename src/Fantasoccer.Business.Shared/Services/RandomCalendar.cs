using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    internal sealed class RandomCalendar : IRandomCalendar
    {
        public List<RandomCalendarDay> GetCalendar(int teamInGame, int round)
        {
            var dayToCreate = teamInGame - 1;
            var numberOfGameInSingleDay = teamInGame / 2;
restart: var baseCalendar = GetCalendarBase(teamInGame);
            List<RandomCalendarDay> games = new();
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
                    games.Add(new RandomCalendarDay
                    {
                        Day = i,
                        Round = round,
                        Game = new RandomCalendarGame
                        {
                            ATeam = value,
                            BTeam = value2,
                            Row = j
                        }
                    });
                }
            }
            return games;
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
        public List<AnnualTeam> Randomize(List<AnnualTeam> teams)
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
    }
}
