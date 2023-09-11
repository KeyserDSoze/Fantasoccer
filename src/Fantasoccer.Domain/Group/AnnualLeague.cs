namespace Fantasoccer
{
    public class AnnualLeague
    {
        public required int Year { get; set; }
        public required LeagueSetting Settings { get; set; }
        public required List<string> BasketsId { get; set; }
    }
}
