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
            var allPlayers = players.OrderByDescending(x => x.FinalValue?.Value ?? -1000).ThenByDescending(x => x.Vote?.Value ?? -1000).ToList();
            var goalkeepers = allPlayers.Where(x => x.Current.Role == Role.GoalKeeper).ToList();
            goalkeepers[0].CurrentPosition = FantaSoccerRole.GoalKeeper;
            goalkeepers[0].Current.Position = FantaSoccerRole.GoalKeeper;
            goalkeepers[1].Current.Position = FantaSoccerRole.BackupGoalKeeper;
            goalkeepers[2].Current.Position = FantaSoccerRole.Tribune;
            List<EnrichedPlayer> refactoredTeam = [.. goalkeepers];
            Change(allPlayers.Where(x => x.Current.Role == Role.Defensor).Take(3), FantaSoccerRole.Defensor);
            Change(allPlayers.Where(x => x.Current.Role == Role.Midfielder).Take(3), FantaSoccerRole.Midfielder);
            Change(allPlayers.Where(x => x.Current.Role == Role.Forward).Take(1), FantaSoccerRole.Forward);
            void Change(IEnumerable<EnrichedPlayer> players, FantaSoccerRole role)
            {
                foreach (var player in players)
                {
                    player.CurrentPosition = role;
                    player.Current.Position = role;
                    refactoredTeam.Add(player);
                }
            }
            foreach (var player in refactoredTeam)
            {
                allPlayers.Remove(player);
            }
            var maxInField = 3;
            var maxDefenders = 2;
            var maxMidfielders = 2;
            var maxForwards = 2;
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
            var firstBackupDefensor = allPlayers.Where(x => x.Current.Role == Role.Defensor).First();
            var secondBackupDefensor = allPlayers.Where(x => x.Current.Role == Role.Defensor).Skip(1).First();
            var firstBackupMidfielder = allPlayers.Where(x => x.Current.Role == Role.Midfielder).First();
            var secondBackupMidfielder = allPlayers.Where(x => x.Current.Role == Role.Midfielder).Skip(1).First();
            var firstBackupForward = allPlayers.Where(x => x.Current.Role == Role.Forward).First();
            var secondBackupForward = allPlayers.Where(x => x.Current.Role == Role.Forward).Skip(1).First();
            firstBackupDefensor.CurrentPosition = FantaSoccerRole.FirstBackupDefensor;
            firstBackupDefensor.Current.Position = FantaSoccerRole.FirstBackupDefensor;
            refactoredTeam.Add(firstBackupDefensor);
            secondBackupDefensor.CurrentPosition = FantaSoccerRole.SecondBackupDefensor;
            secondBackupDefensor.Current.Position = FantaSoccerRole.SecondBackupDefensor;
            refactoredTeam.Add(secondBackupDefensor);
            firstBackupMidfielder.CurrentPosition = FantaSoccerRole.FirstBackupMidfielder;
            firstBackupMidfielder.Current.Position = FantaSoccerRole.FirstBackupMidfielder;
            refactoredTeam.Add(firstBackupMidfielder);
            secondBackupMidfielder.CurrentPosition = FantaSoccerRole.SecondBackupMidfielder;
            secondBackupMidfielder.Current.Position = FantaSoccerRole.SecondBackupMidfielder;
            refactoredTeam.Add(secondBackupMidfielder);
            firstBackupForward.CurrentPosition = FantaSoccerRole.FirstBackupForward;
            firstBackupForward.Current.Position = FantaSoccerRole.FirstBackupForward;
            refactoredTeam.Add(firstBackupForward);
            secondBackupForward.CurrentPosition = FantaSoccerRole.SecondBackupForward;
            secondBackupForward.Current.Position = FantaSoccerRole.SecondBackupForward;
            refactoredTeam.Add(secondBackupForward);
            foreach (var player in refactoredTeam)
            {
                allPlayers.Remove(player);
            }
            foreach (var player in allPlayers)
            {
                player.CurrentPosition = FantaSoccerRole.Tribune;
                player.Current.Position = FantaSoccerRole.Tribune;
                refactoredTeam.Add(player);
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
