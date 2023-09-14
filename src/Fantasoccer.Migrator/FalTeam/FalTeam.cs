namespace Fantasoccer.Migrator.FalTeam
{
    public class RootTeam
    {
        public TeamX Value { get; set; }
    }
    public class TeamX
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public List<string> AdditionalOwners { get; set; }
        public List<PlayerX> Players { get; set; }
        public int RevenueMoney { get; set; }
        public int MoneyFromRank { get; set; }
    }
    public class PlayerX : RealPlayerX
    {
        public int Price { get; set; }
        public PlayerInTeamStatus Status { get; set; }
        public FantaSoccerRole Position { get; set; }
    }
    public class RealPlayerX
    {
        public string Name { get; set; }
        public RealTeamX RealTeam { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class RealTeamX
    {
        public required string Name { get; set; }
    }
}
