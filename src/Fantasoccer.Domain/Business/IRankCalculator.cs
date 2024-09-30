namespace Fantasoccer.Business
{
    public interface IRankCalculator
    {
        Rank AddDay(LeagueSetting leagueSetting, Rank rank, Dictionary<string, Day> rounds);
        bool AddDay(RankedTeam team, bool isInHome, GameResult? result, LeagueSetting leagueSetting);
    }
}
