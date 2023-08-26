using RepositoryFramework;

namespace Fantasoccer
{
    public class YearDayLeague : IDefaultKey
    {
        public int Year { get; set; }
        public int Day { get; set; }
        public string Group { get; set; }
        public string League { get; set; }
    }
}
