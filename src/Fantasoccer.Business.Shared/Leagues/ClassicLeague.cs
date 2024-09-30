using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class ClassicLeague : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;
        private readonly IRandomCalendar _randomCalendar;
        private readonly ILeagueCalculator? _defaultLeagueCalculator;
        public ClassicLeague(IRankCalculator rankCalculator, IFactory<ILeagueCalculator> factory, IRandomCalendar randomCalendar)
        {
            _rankCalculator = rankCalculator;
            _randomCalendar = randomCalendar;
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
    }
}
