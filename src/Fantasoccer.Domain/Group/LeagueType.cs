namespace Fantasoccer
{
    public enum LeagueType
    {
        Null,
        League,
        Cup,
        NewCup,
        SuperLeague
    }
    public static class LeagueTypeExtensions
    {
        public static LeagueType[] All { get; } = (LeagueType[])Enum.GetValues(typeof(LeagueType));
    }
}
