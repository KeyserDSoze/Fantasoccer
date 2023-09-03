namespace Fantasoccer
{
    public class AppIdentity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public List<GroupForIdentity> Groups { get; set; }
    }
    public sealed class GroupForIdentity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IdentityRole Role { get; set; }
        public List<LeagueForIdentity> Leagues { get; set; }
    }
    [Flags]
    public enum IdentityRole
    {
        Reader = 1,
        Participant = 2,
        Admin = 4,
        Old = 8,
    }
    public sealed class LeagueForIdentity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IdentityRole Role { get; set; }
        public LeagueType Type { get; set; }
    }
}
