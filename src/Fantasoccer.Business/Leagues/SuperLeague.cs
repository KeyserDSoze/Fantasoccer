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
            if (goalkeepers.Count > 0)
                SetPosition(goalkeepers[0], FantaSoccerRole.GoalKeeper);
            if (goalkeepers.Count > 1)
                SetPosition(goalkeepers[1], FantaSoccerRole.BackupGoalKeeper);
            foreach (var goalkeeper in goalkeepers.Skip(2))
                SetPosition(goalkeeper, FantaSoccerRole.Tribune);
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
            AddPlayers(allPlayers.Where(x => x.Vote?.Value >= 6));
            if (maxInField > 0)
            {
                AddPlayers(allPlayers.OrderByDescending(x => x.FinalValue?.Value ?? -1000).ThenByDescending(x => x.Current.Role).ThenByDescending(x => x.Vote?.Value ?? -1000));
            }
            void AddPlayers(IEnumerable<EnrichedPlayer> checkedPlayers)
            {
                foreach (var player in checkedPlayers)
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
            }
            AddBackups(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor);
            AddBackups(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, FantaSoccerRole.SecondBackupMidfielder);
            AddBackups(Role.Forward, FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward);
            void AddBackups(Role role, FantaSoccerRole firstPosition, FantaSoccerRole secondPosition)
            {
                var backups = allPlayers.Where(x => x.Current.Role == role).Take(2).ToList();
                if (backups.Count > 0)
                {
                    SetPosition(backups[0], firstPosition);
                    refactoredTeam.Add(backups[0]);
                }
                if (backups.Count > 1)
                {
                    SetPosition(backups[1], secondPosition);
                    refactoredTeam.Add(backups[1]);
                }
            }
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

            static void SetPosition(EnrichedPlayer player, FantaSoccerRole position)
            {
                player.CurrentPosition = position;
                player.Current.Position = position;
            }
        }
        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
            => _defaultLeagueCalculator!.GetRandomCalendar(year, league, teams);
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
            => calendar;
        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
            => _defaultLeagueCalculator!.GetTheYearlyWinners(rank, calendar);
    }
}
