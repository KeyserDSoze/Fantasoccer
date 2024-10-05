namespace Fantasoccer.Applications.App.Models
{
    public sealed class BuyPlayerModalModel
    {
        public RealPlayer Player { get; set; }
        public int Price { get; set; }
        public List<Team> Teams { get; set; }
        public Team Team { get; set; }
    }
}
