namespace FantacalcioV4
{
    public enum FantaSoccerRole
    {
        All = -2,
        Invasion = -1,
        GoalKeeper,
        Defensor,
        Midfielder,
        Forward,
        BackupGoalKeeper,
        FirstBackupDefensor,
        SecondBackupDefensor,
        FirstBackupMidfielder,
        SecondBackupMidfielder,
        FirstBackupForward,
        SecondBackupForward,
        Tribune
    }
    public static class TransformationRole
    {
        public static Role ToRole(this FantaSoccerRole fantaSoccerRole)
        {
            switch (fantaSoccerRole)
            {
                default:
                case FantaSoccerRole.All:
                case FantaSoccerRole.Invasion:
                    return Role.Undefined;
                case FantaSoccerRole.GoalKeeper:
                case FantaSoccerRole.BackupGoalKeeper:
                    return Role.GoalKeeper;
                case FantaSoccerRole.Defensor:
                case FantaSoccerRole.FirstBackupDefensor:
                case FantaSoccerRole.SecondBackupDefensor:
                    return Role.Defensor;
                case FantaSoccerRole.Midfielder:
                case FantaSoccerRole.FirstBackupMidfielder:
                case FantaSoccerRole.SecondBackupMidfielder:
                    return Role.Midfielder;
                case FantaSoccerRole.Forward:
                case FantaSoccerRole.FirstBackupForward:
                case FantaSoccerRole.SecondBackupForward:
                    return Role.Forward;

            }
        }
    }
}