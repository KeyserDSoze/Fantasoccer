namespace Fantasoccer
{
    public sealed class PushNotificationSent
    {
        public bool DeploymentReminder { get; set; }
        public bool EndDay { get; set; }
        public bool LiveChangesReminder { get; set; }
        public List<string> LiveFormationChangeNotifications { get; set; } = [];
    }
}
