namespace Fantasoccer.Domain
{
    public sealed class News
    {
        public string League { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime Date { get; set; }
        public string? Image { get; set; }
    }
}
