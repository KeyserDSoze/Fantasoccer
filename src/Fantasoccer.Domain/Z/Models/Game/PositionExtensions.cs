using System.Collections.Generic;
using System.Linq;

namespace Fantasoccer
{
    public static class PositionExtensions
    {
        public static bool ExistsRightPlayersInRightPosition(this IEnumerable<Player> players)
        {
            IEnumerable<(FantaSoccerRole Status, IEnumerable<(Role Role, int Count)> PlayersByRole)> values =
                from player in players
                group player by player.Position?.Status ?? FantaSoccerRole.Tribune
                into status
                select (status.Key,
                        from playerByStatus in status
                        group playerByStatus by playerByStatus.Role
                        into roles
                        select (roles.Key, roles.Count())
                );
            int startingTeamCounter = values.FirstOrDefault(x => x.Status == FantaSoccerRole.GoalKeeper).PlayersByRole.Sum(x => x.Count) +
                                      values.FirstOrDefault(x => x.Status == FantaSoccerRole.Defensor).PlayersByRole.Sum(x => x.Count) +
                                      values.FirstOrDefault(x => x.Status == FantaSoccerRole.Midfielder).PlayersByRole.Sum(x => x.Count) +
                                      values.FirstOrDefault(x => x.Status == FantaSoccerRole.Forward).PlayersByRole.Sum(x => x.Count);
            if (startingTeamCounter != 11)
                return false;
            foreach (var value in values)
            {
                if (value.Status != FantaSoccerRole.Tribune && value.PlayersByRole.Count() > 1)
                    return false;
                switch (value.Status)
                {
                    case FantaSoccerRole.GoalKeeper:
                    case FantaSoccerRole.BackupGoalKeeper:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.GoalKeeper)
                            return false;
                        if (value.PlayersByRole.FirstOrDefault().Count != 1)
                            return false;
                        break;
                    case FantaSoccerRole.Defensor:
                    case FantaSoccerRole.FirstBackupDefensor:
                    case FantaSoccerRole.SecondBackupDefensor:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Defensor)
                            return false;
                        if (value.Status == FantaSoccerRole.Defensor && (value.PlayersByRole.FirstOrDefault().Count < 2 || value.PlayersByRole.FirstOrDefault().Count > 5))
                            return false;
                        if (value.Status != FantaSoccerRole.Defensor && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return false;
                        break;
                    case FantaSoccerRole.Midfielder:
                    case FantaSoccerRole.FirstBackupMidfielder:
                    case FantaSoccerRole.SecondBackupMidfielder:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Midfielder)
                            return false;
                        if (value.Status == FantaSoccerRole.Midfielder && (value.PlayersByRole.FirstOrDefault().Count < 2 || value.PlayersByRole.FirstOrDefault().Count > 5))
                            return false;
                        if (value.Status != FantaSoccerRole.Midfielder && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return false;
                        break;
                    case FantaSoccerRole.Forward:
                    case FantaSoccerRole.FirstBackupForward:
                    case FantaSoccerRole.SecondBackupForward:
                        if (value.PlayersByRole.FirstOrDefault().Role != Role.Forward)
                            return false;
                        if (value.Status == FantaSoccerRole.Forward && (value.PlayersByRole.FirstOrDefault().Count < 1 || value.PlayersByRole.FirstOrDefault().Count > 3))
                            return false;
                        if (value.Status != FantaSoccerRole.Forward && value.PlayersByRole.FirstOrDefault().Count != 1)
                            return false;
                        break;
                    case FantaSoccerRole.Tribune:
                        if (value.PlayersByRole.Sum(x => x.Count) != 7)
                            return false;
                        break;
                    case FantaSoccerRole.All:
                        return false;
                }
            }
            return true;
        }
    }
}