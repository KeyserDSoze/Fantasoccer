namespace Fantasoccer.Domain
{
    public sealed class NewsWrapper
    {
        public int Year { get; set; }
        public int Day { get; set; }
        public string Group { get; set; }
        public List<News> News { get; set; }
    }
}
