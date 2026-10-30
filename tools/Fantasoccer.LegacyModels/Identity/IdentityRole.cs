namespace Fantasoccer
{
    [Flags]
    public enum IdentityRole
    {
        None = 0,
        Reader = 1,
        Participant = 2,
        Admin = 4,
        SuperAdmin = 8
    }
}
