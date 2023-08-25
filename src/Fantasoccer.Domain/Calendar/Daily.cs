namespace Fantasoccer
{
    public class Daily
    {
        public int SerieADay { get; set; }
        public Dictionary<int, List<CalendarGame>> Games { get; set; }
    }
}
