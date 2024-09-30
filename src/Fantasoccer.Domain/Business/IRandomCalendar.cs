using System.Security.Cryptography;

namespace Fantasoccer.Business
{
    public interface IRandomCalendar
    {
        List<RandomCalendarDay> GetCalendar(int teamInGame, int round);
        List<AnnualTeam> Randomize(List<AnnualTeam> teams);
    }
}
