using Blazorise;

namespace Fantasoccer.Applications.App
{
    public sealed class FantaSoccerPosition
    {
        public FantaSoccerRole Position { get; }
        public Role Role { get; }
        public string Css { get; }
        public Func<Player, bool> Filter { get; }

        public FantaSoccerPosition(Role role, FantaSoccerRole fantaSoccerRole, string css, Func<Player, bool>? filter = null)
        {
            Role = role;
            Position = fantaSoccerRole;
            Css = css;
            Filter = filter ?? (item => item.Role == role);
        }
        public static List<FantaSoccerPosition> Default { get; } = new()
        {
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, "playerVerticalAlign"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.Defensor, "playerVerticalAlign"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.Midfielder, "playerVerticalAlign"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.Forward, "playerVerticalAlign"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.BackupGoalKeeper, "playerVerticalAlign firstBackupContainer"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, "playerVerticalAlign firstBackupContainer"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, "playerVerticalAlign firstBackupContainer"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.FirstBackupForward, "playerVerticalAlign firstBackupContainer"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.All, "playerVerticalAlign invisible secondBackupContainer"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.SecondBackupDefensor, "playerVerticalAlign secondBackupContainer"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder, "playerVerticalAlign secondBackupContainer"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.SecondBackupForward, "playerVerticalAlign secondBackupContainer"),
            new FantaSoccerPosition(Role.Undefined, FantaSoccerRole.Tribune, "playerVerticalAlign tribuneContainer", x => true),

        };
    }
}
