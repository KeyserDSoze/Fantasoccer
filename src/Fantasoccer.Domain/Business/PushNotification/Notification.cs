namespace Fantasoccer
{
    public sealed class Notification
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public NotificationData Data { get; set; }
        public string? Icon { get; set; }
        public string? Badge { get; set; }
        public string? Sound { get; set; }
        public string? Tag { get; set; }
        public bool RequireInteraction { get; set; }
    }
}
