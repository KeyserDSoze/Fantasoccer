using System;

namespace FantacalcioV4
{
    public static class TeamValue
    {
        private static Func<string, string> GetTeamName;
        public static void SetTeamName(Func<string, string> getTeamName) 
            => GetTeamName = getTeamName;
        public static string NameInstance(string id) 
            => GetTeamName(id);
        private static Func<string, int, Team> GetTeam;
        public static void SetTeam(Func<string, int, Team> getTeam)
            => GetTeam = getTeam;
        public static Team Instance(string id, int serieADay)
            => GetTeam(id, serieADay);
    }
}