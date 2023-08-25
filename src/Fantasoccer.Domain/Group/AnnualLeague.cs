namespace Fantasoccer
{
    public class AnnualLeague
    {
        public string Id { get; set; }
        public LeagueType Type { get; set; }
        public LeagueSetting Settings { get; set; }
        public List<AnnualTeam> Teams { get; set; }
    }
}
