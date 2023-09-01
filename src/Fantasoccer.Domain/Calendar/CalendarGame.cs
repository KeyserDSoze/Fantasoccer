namespace Fantasoccer
{
    public class CalendarGame
    {
        public string Id { get; set; }
        public int Number { get; set; }
        public string Home { get; set; }
        public string HomeOwner { get; set; }
        public string Away { get; set; }
        public string AwayOwner { get; set; }
        public GameResult? Result { get; set; }
    }
}
