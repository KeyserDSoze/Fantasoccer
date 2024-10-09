namespace Fantasoccer.Application.Shared
{
    public sealed class PushNotificationSubscription
    {
        public string Endpoint { get; set; }
        public PushNotificationSubscriptionKeys Keys { get; set; }
    }
}
