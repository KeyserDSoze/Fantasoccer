using Blazorise;

namespace Fantasoccer.Applications.App
{
    public sealed class FantaSoccerPosition
    {
        public FantaSoccerRole Position { get; }
        public Role Role { get; }
        public string Css { get; }
        public string ColumnContainer { get; }
        public Func<EnrichedPlayer, bool> Filter { get; }

        public FantaSoccerPosition(Role role, FantaSoccerRole fantaSoccerRole, string css, string columnContainer, Func<EnrichedPlayer, bool>? filter = null)
        {
            Role = role;
            Position = fantaSoccerRole;
            Css = css;
            ColumnContainer = columnContainer;
            Filter = filter ?? (item => item.Current.Role == role);
        }
        public static List<FantaSoccerPosition> Default { get; } = new()
        {
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.Defensor, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.Midfielder, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.Forward, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.BackupGoalKeeper, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.FirstBackupForward, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.All, "playerVerticalAlign invisible secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.SecondBackupDefensor, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.SecondBackupForward, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Undefined, FantaSoccerRole.Tribune, "playerVerticalAlign tribuneContainer", "columnContainer columnContainerWithBlackBackground", x => true),
        };
        public static List<FantaSoccerPosition> Inverted { get; } = new()
        {
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.Forward, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.Midfielder, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.Defensor, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, "playerVerticalAlign", "columnContainer columnContainerWithWhiteBackground"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.FirstBackupForward, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.BackupGoalKeeper, "playerVerticalAlign firstBackupContainer", "columnContainerBench columnContainerWithRedBackground"),
            new FantaSoccerPosition(Role.Forward, FantaSoccerRole.SecondBackupForward, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Defensor, FantaSoccerRole.SecondBackupDefensor, "playerVerticalAlign secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.GoalKeeper, FantaSoccerRole.All, "playerVerticalAlign invisible secondBackupContainer", "columnContainerBench columnContainerWithVioletBackground"),
            new FantaSoccerPosition(Role.Undefined, FantaSoccerRole.Tribune, "playerVerticalAlign tribuneContainerInverted", "columnContainer columnContainerWithBlackBackground", x => true),
        };
    }
}
