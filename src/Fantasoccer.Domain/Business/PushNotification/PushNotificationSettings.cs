using WebPush;

namespace Fantasoccer
{
    public sealed class PushNotificationSettings
    {
        public bool MatchEvents { get; set; } = true;
        public bool OpponentMatchEvents { get; set; }
        public bool DeploymentReminder { get; set; }
        public bool EndDay { get; set; }
        public bool MarketEvents { get; set; }
        public bool LiveChangesReminder { get; set; }
        public bool LiveFormationChangeEvents { get; set; }
        public List<PushSubscription> Subscriptions { get; set; } = new();
    }
}
