using Fantasoccer.Business;

namespace Fantasoccer
{
    public interface ILeagueCalculator
    {
        Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, List<string>? avoidCalculationRounds = null);
        IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator);
        IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players);
        Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams);
        Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar);
        List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar);
    }
}
