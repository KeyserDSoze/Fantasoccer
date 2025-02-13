namespace Fantasoccer
{
    public enum Role
    {
        Undefined = -1,
        GoalKeeper,
        Defensor,
        Midfielder,
        Forward
    }
    public static class RoleExtensions
    {
        public static string AsString(this Role role)
        {
            return role switch
            {
                Role.GoalKeeper => "Portiere",
                Role.Defensor => "Difensore",
                Role.Midfielder => "Centrocampista",
                Role.Forward => "Attaccante",
                _ => "Non definito"
            };
        }
    }
}
