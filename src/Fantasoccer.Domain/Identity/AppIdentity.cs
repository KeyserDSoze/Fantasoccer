namespace Fantasoccer
{
    public class AppIdentity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public AppIdentityState State { get; set; } = new();
        public List<GroupForIdentity> Groups { get; set; }
    }
}
