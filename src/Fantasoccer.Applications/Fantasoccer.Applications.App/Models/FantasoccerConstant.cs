namespace Fantasoccer.Applications.App
{
    public static class FantasoccerConstant
    {
        public static List<RoleVisualizer> Roles { get; } = new List<RoleVisualizer>()
        {
            new("GK","GoalKeeper", Role.GoalKeeper),
            new("DF","Defensor", Role.Defensor),
            new("MD","Midfielder", Role.Midfielder),
            new("FD","Forward", Role.Forward),
        };
    }
}
