using RepositoryFramework;

namespace Fantasoccer
{
    public class TeamKey : IDefaultKey
    {
        public string Group { get; set; }
        public int Year { get; set; }
        public string Email { get; set; }
    }
}
