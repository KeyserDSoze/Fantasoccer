namespace Fantasoccer
{
    public interface IPushNotificationService
    {
        ValueTask SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message);
        PushNotificationStatistics GetStatistics();
        void ResetStatistics();
    }

    public class PushNotificationStatistics
    {
        public int TotalAttempted { get; set; }
        public int TotalSucceeded { get; set; }
        public int TotalFailed { get; set; }
        public int TotalDiscarded { get; set; }
    }
}
