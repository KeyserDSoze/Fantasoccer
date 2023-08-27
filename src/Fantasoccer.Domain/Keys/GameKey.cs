using RepositoryFramework;

namespace Fantasoccer
{
    public class GameKey : IDefaultKey
    {
        public string Group { get; set; }
        public string League { get; set; }
        public int Year { get; set; }
        public int Day { get; set; }
        public string GameId { get; set; }
    }
}
