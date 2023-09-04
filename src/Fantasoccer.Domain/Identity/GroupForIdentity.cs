namespace Fantasoccer
{
    public sealed class GroupForIdentity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TeamName { get; set; }
        public IdentityRole Role { get; set; }
        public List<LeagueForIdentity> Leagues { get; set; }
    }
}
