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
            var key = ((char)(64)).ToString();
            var allTeams = teams.ToList();
            var randomizedTeams = _randomCalendar.Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };

            var randomizedCalendar = _randomCalendar.GetCalendar(randomizedTeams.Count, 1);
            var numberOfDays = randomizedTeams.Count - 1;
            var homeAndAwayCounts = (38 - league.Settings.DelayedDay) / numberOfDays;
            var actualDay = 1;
            for (var homeAwayRound = 0; homeAwayRound < homeAndAwayCounts; homeAwayRound++)
            {
                if (!calendar.Rounds.ContainsKey(key))
                    calendar.Rounds.Add(key, []);
                var singleRound = calendar.Rounds[key];
                foreach (var rounds in randomizedCalendar.GroupBy(x => x.Day))
                {
                    foreach (var games in rounds.GroupBy(x => x.Round))
                    {
                        var serieADay = actualDay + league.Settings.DelayedDay;
                        var isCancelled = actualDay <= league.Settings.CancelledDay;
                        var day = new Day { Games = [], SerieADay = serieADay, Number = actualDay };
                        singleRound.Add(day);
                        var gameCount = 1;
                        foreach (var game in games)
                        {
                            var home = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.ATeam] : randomizedTeams[game.Game.BTeam];
                            var away = homeAwayRound % 2 == 0 ? randomizedTeams[game.Game.BTeam] : randomizedTeams[game.Game.ATeam];
                            day.Games.Add(new CalendarGame
                            {
                                Id = Guid.NewGuid().ToString(),
                                Number = gameCount,
                                Home = home.Name,
                                HomeOwner = home.Owner,
                                Away = away.Name,
                                AwayOwner = away.Owner,
                                Result = isCancelled ? new GameResult { IsCancelled = true } : null
                            });
                            gameCount++;
                        }
                        actualDay++;
                    }
                }
            }
            return calendar;
        }
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
            => calendar;
    }
}
