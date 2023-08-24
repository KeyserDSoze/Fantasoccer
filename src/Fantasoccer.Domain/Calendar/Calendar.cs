using RepositoryFramework;

namespace Fantasoccer
{
    public class Calendar
    {
        public string LeagueName { get; set; }
        public string GroupName { get; set; }
        public int Year { get; set; }
    }
    public class CalendaryKey : IKey
    {
        public string LeagueId { get; set; }
        public string GroupId { get; set; }
        public int Year { get; set; }
        public static IKey Parse(string keyAsString)
        {
            throw new NotImplementedException();
        }

        public string AsString()
        {
            throw new NotImplementedException();
        }
    }
}
