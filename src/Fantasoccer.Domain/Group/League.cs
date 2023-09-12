namespace Fantasoccer
{
    public class League
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsMain { get; set; }
        public LeagueType Type { get; set; }
        public List<AnnualLeague> Years { get; set; }
        public required List<string> BasketsId { get; set; }
    }
}
