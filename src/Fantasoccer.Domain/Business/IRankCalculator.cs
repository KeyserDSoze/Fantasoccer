namespace Fantasoccer
{
    public interface IRankCalculator
    {
        Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, LeagueType type);
    }
}
