using Rystem.Security.Cryptography;

namespace Fantasoccer
{
    public static class FantacalcioId
    {
        public static string GetPlayerKey(int year, string name)
            => KeyUtils.GetPlayerKey(year, name);
        public static string GetPlayedDay(string group, int year, string league, int day)
            => $"{GetLeague(group, year, league)}/Day/{day}";
        public static string GetRank(string group, int year, string league)
            => $"{GetGroupByYear(group, year)}/Rank/{league}";
        public static string GetCalendar(string group, int year, string league)
            => $"{GetGroupByYear(group, year)}/Calendar/{league}";
        public static string GetLeague(string group, int year, string league)
            => $"{GetGroupByYear(group, year)}/League/{league}";
        public static string GetTeam(string group, int year, string username)
            => $"{GetGroupByYear(group, year)}/User/{username}";
        public static string GetGroup(string group)
            => $"Group/{group}";
        public static string GetGroupByYear(string group, int year)
            => $"Group/{group}/Year/{(year == 0 ? "{0}" : year)}";
        public static string GetIdentity(string username)
            => $"Identities/{username}";
        public static string GetAnnualPlayers(int year)
            => $"AllPlayers/{year}";
        public static string GetSerieACalendar(int year)
            => $"SerieA/Calendar/{year}";
        public static string GetLiveVotes(int year, int serieADay)
            => $"Vote/Live/{year}/{serieADay}";
        public static string GetFinalVotes(int year, int serieADay)
            => $"Vote/Final/{year}/{serieADay}";
        public static string GetGameId(string group, int year, string league, int serieADay, string homeUsername, string awayUsername)
            => $"{GetLeague(group, year, league)}/Game/{serieADay}/Home/{homeUsername}/Away/{awayUsername}";
    }
}
