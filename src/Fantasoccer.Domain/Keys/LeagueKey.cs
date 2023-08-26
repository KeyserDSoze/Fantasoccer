using RepositoryFramework;

namespace Fantasoccer
{
    public class LeagueKey : IDefaultKey
    {
        public int Year { get; set; }
        public string Group { get; set; }
        public string League { get; set; }
    }
}
