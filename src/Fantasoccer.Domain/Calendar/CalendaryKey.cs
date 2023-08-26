using RepositoryFramework;

namespace Fantasoccer
{
    public class CalendaryKey : IDefaultKey
    {
        public string GroupId { get; set; }
        public string LeagueId { get; set; }
        public int Year { get; set; }
    }
}
