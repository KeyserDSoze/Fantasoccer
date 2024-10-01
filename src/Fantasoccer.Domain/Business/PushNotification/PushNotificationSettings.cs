using Microsoft.Extensions.Logging;
using WebPush;

namespace Fantasoccer
{
    public sealed class PushNotificationSettings
    {
        public bool MatchEvents { get; set; }
        public bool DeploymentReminder { get; set; }
        public bool EndDay { get; set; }
        public bool MarketEvents { get; set; }
        public List<PushSubscription> Subscriptions { get; set; } = new();
    }
}
