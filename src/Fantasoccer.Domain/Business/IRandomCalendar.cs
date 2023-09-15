namespace Fantasoccer
{
    public interface IRandomCalendar
    {
        Calendar GetRandomCalendar(int year, LeagueType type, AnnualLeague league, IEnumerable<AnnualTeam> teams);
        Calendar AddDaysInLeague(int year, LeagueType type, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar);
    }
}
