namespace Fantasoccer
{
    public sealed class LeagueForIdentity
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required LeagueType Type { get; set; }
        public required List<string> BasketsId { get; set; }
    }
}
