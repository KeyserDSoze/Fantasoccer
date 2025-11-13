using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class ClassicLeague : ILeagueCalculator
    {
        private readonly ILeagueCalculator? _defaultLeagueCalculator;
        public ClassicLeague(IFactory<ILeagueCalculator> factory)
        {
            _defaultLeagueCalculator = factory.Create(string.Empty);
        }
        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, List<string>? avoidCalculationRounds = null)
        {
            if (avoidCalculationRounds != null)
            {
                return _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, avoidCalculationRounds);
            }
            else
                return _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, null);
        }
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator)
           => _defaultLeagueCalculator!.GetRightFormation(players, teamCalculator);
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            return _defaultLeagueCalculator!.GetRightFormation(players);
        }
        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            return _defaultLeagueCalculator!.GetRandomCalendar(year, league, teams);
        }
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
            => calendar;

        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
            => _defaultLeagueCalculator!.GetTheYearlyWinners(rank, calendar);
    }
}
