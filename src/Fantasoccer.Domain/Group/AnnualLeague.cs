namespace Fantasoccer
{
    public class AnnualLeague
    {
        public string Id { get; set; }
        public int Year { get; set; }
        public LeagueSetting Settings { get; set; }
        public List<AnnualTeam> Teams { get; set; }
    }
}
