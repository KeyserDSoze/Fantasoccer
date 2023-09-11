namespace Fantasoccer
{
    public interface IRandomCalendar
    {
        Calendar GetRandomCalendar(int year, LeagueType type, AnnualLeague league, List<AnnualTeam> teams);
    }
}
