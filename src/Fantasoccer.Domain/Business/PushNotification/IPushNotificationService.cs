namespace Fantasoccer
{
    public interface IPushNotificationService
    {
        ValueTask<PushNotificationDeliveryResult> SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message, CancellationToken cancellationToken = default);
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
