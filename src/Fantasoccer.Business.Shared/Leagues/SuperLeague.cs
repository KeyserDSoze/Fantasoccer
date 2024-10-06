using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class SuperLeague : ILeagueCalculator
    {
        private readonly ILeagueCalculator? _defaultLeagueCalculator;
        public SuperLeague(IFactory<ILeagueCalculator> factory)
        {
            _defaultLeagueCalculator = factory.Create(string.Empty);
        }
        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, List<string>? avoidCalculationRounds = null)
        {
            var rank = (avoidCalculationRounds != null) ?
                _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, avoidCalculationRounds) :
                _defaultLeagueCalculator!.GetRank(leagueSetting, calendar, null);
            return rank;
        }
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator)
           => GetRightFormation(teamCalculator.Enrich(players));
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            if (!players.Any())
                return Array.Empty<EnrichedPlayer>();
            var allPlayers = players.OrderByDescending(x => x.FinalValue?.Value ?? -1000).ToList();
            var goalkeepers = allPlayers.Where(x => x.Current.Role == Role.GoalKeeper).ToList();
            goalkeepers[0].CurrentPosition = FantaSoccerRole.GoalKeeper;
            goalkeepers[1].CurrentPosition = FantaSoccerRole.BackupGoalKeeper;
            goalkeepers[2].CurrentPosition = FantaSoccerRole.Tribune;
            List<EnrichedPlayer> refactoredTeam = [.. goalkeepers,
            .. allPlayers.Where(x => x.Current.Role == Role.Defensor).Take(3),
            .. allPlayers.Where(x => x.Current.Role == Role.Midfielder).Take(3),
            .. allPlayers.Where(x => x.Current.Role == Role.Forward).Take(1)];
            foreach (var player in refactoredTeam)
            {
                allPlayers.Remove(player);
            }
            var maxInField = 10;
            var maxDefenders = 2;
            var maxMidfielders = 2;
            var maxForwards = 2;
            var defenderBackup = 2;
            var midfielderBackup = 2;
            var forwardBackup = 2;
            foreach (var player in allPlayers)
            {
                if (player.Current.Role == Role.Defensor && maxDefenders > 0)
                {
                    player.CurrentPosition = FantaSoccerRole.Defensor;
                    player.Current.Position = FantaSoccerRole.Defensor;
                    refactoredTeam.Add(player);
                    maxDefenders--;
                    maxInField--;
                }
                else if (player.Current.Role == Role.Midfielder && maxMidfielders > 0)
                {
                    player.CurrentPosition = FantaSoccerRole.Midfielder;
                    player.Current.Position = FantaSoccerRole.Midfielder;
                    refactoredTeam.Add(player);
                    maxMidfielders--;
                    maxInField--;
                }
                else if (player.Current.Role == Role.Forward && maxForwards > 0)
                {
                    player.CurrentPosition = FantaSoccerRole.Forward;
                    player.Current.Position = FantaSoccerRole.Forward;
                    refactoredTeam.Add(player);
                    maxForwards--;
                    maxInField--;
                }
                if (maxInField == 0)
                {
                    break;
                }
            }
            foreach (var player in refactoredTeam)
            {
                allPlayers.Remove(player);
            }
            foreach (var player in allPlayers.Where(x => x.Current.Role != Role.GoalKeeper))
            {
                if (player.Current.Role == Role.Defensor && defenderBackup > 0)
                {
                    player.CurrentPosition = defenderBackup == 2 ? FantaSoccerRole.FirstBackupDefensor : FantaSoccerRole.SecondBackupDefensor;
                    player.Current.Position = player.CurrentPosition;
                    refactoredTeam.Add(player);
                    defenderBackup--;
                }
                else if (player.Current.Role == Role.Midfielder && midfielderBackup > 0)
                {
                    player.CurrentPosition = midfielderBackup == 2 ? FantaSoccerRole.FirstBackupMidfielder : FantaSoccerRole.SecondBackupMidfielder;
                    player.Current.Position = player.CurrentPosition;
                    refactoredTeam.Add(player);
                    midfielderBackup--;
                }
                else if (player.Current.Role == Role.Forward && forwardBackup > 0)
                {
                    player.CurrentPosition = forwardBackup == 2 ? FantaSoccerRole.FirstBackupForward : FantaSoccerRole.SecondBackupForward;
                    player.Current.Position = player.CurrentPosition;
                    refactoredTeam.Add(player);
                    forwardBackup--;
                }
                else
                {
                    player.CurrentPosition = FantaSoccerRole.Tribune;
                    player.Current.Position = player.CurrentPosition;
                    refactoredTeam.Add(player);
                }
            }
            return refactoredTeam;
        }
        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
            => _defaultLeagueCalculator!.GetRandomCalendar(year, league, teams);
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
            => calendar;
        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
            => _defaultLeagueCalculator!.GetTheYearlyWinners(rank, calendar);
    }
}
