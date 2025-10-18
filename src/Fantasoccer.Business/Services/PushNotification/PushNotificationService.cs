using System.Text.Json;
using RepositoryFramework;
using WebPush;

namespace Fantasoccer.Business
{
    internal class PushNotificationService : IPushNotificationService
    {
        private const string PublicKey = "BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8";
        private const string PrivateKey = "uPF1e0ItKwn4WRSD8f88M9U43XjUuUCY9Bn1tA0GPWg";
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        public PushNotificationService(IRepository<PushNotificationSettings, string> pushNotificationRepository)
        {
            _pushNotificationRepository = pushNotificationRepository;
        }
        public async ValueTask SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message)
        {
            List<PushSubscription> noLongerValidSubscriptions = new();
            foreach (var notification in notificationSettings.Subscriptions)
            {
                var subscription = new PushSubscription(notification.Endpoint, notification.P256DH, notification.Auth);
                var vapidDetails = new VapidDetails($"mailto:{email.Split('@').First()}@fanta.zone", PublicKey, PrivateKey);
                using var webPushClient = new WebPushClient();
                try
                {
                    var jsonMessage = message.ToJson();
                    await webPushClient.SendNotificationAsync(subscription, jsonMessage, vapidDetails);
                }
                catch (WebPushException wpe)
                {
                    if (wpe.Message.ToLower().Contains("subscription no longer valid"))
                    {
                        noLongerValidSubscriptions.Add(subscription);
                    }
                }
                catch
                {
                }
            }
            if (noLongerValidSubscriptions.Count > 0)
            {
                foreach (var subscription in noLongerValidSubscriptions)
                {
                    notificationSettings.Subscriptions.Remove(notificationSettings.Subscriptions.First(x => x.Endpoint == subscription.Endpoint));
                }
                await _pushNotificationRepository.UpdateAsync(email, notificationSettings);
            }
        }
    }
}
