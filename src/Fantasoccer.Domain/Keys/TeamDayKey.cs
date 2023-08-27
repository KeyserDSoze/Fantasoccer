using RepositoryFramework;

namespace Fantasoccer
{
    public class TeamDayKey : IDefaultKey
    {
        public string Group { get; set; }
        public int Year { get; set; }
        public int Day { get; set; }
        public string Email { get; set; }
    }
}
