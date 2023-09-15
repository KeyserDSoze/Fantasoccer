namespace Fantasoccer
{
    public interface IRankCalculator
    {
        Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, LeagueType type);
        Rank AddDay(LeagueSetting leagueSetting, Rank rank, Dictionary<string, Day> rounds, LeagueType type);
    }
}
