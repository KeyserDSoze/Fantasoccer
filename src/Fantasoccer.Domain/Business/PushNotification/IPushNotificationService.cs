namespace Fantasoccer
{
    public interface IPushNotificationService
    {
        ValueTask SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message);
    }
}
