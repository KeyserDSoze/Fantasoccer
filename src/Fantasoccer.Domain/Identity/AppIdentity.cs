namespace Fantasoccer
{
    public class AppIdentity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public AppIdentityState State { get; set; } = new();
        public List<GroupForIdentity> Groups { get; set; }
    }
    public sealed class AppIdentityState
    {
        public string? GroupId { get; set; }
        public string? LeagueId { get; set; }
        public int? Year { get; set; }
    }
}
