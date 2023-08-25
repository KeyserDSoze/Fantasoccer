namespace Fantasoccer
{
    public record Notification(string Title, string Body, NotificationData Data);
    public record NotificationData(string Uri);
}