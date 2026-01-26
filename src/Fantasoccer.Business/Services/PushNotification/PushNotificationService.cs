using System.Text.Json;
using RepositoryFramework;
using WebPush;

namespace Fantasoccer.Business
{
    internal class PushNotificationService : IPushNotificationService
    {
        private const string PublicKey = "BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8";
        private const string PrivateKey = "uPF1e0ItKwn4WRSD8f88M9U43XjUuUCY9Bn1tA0GPWg";
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        private readonly IFantasoccerLogger _logger;
        private readonly PushNotificationStatistics _statistics = new();

        public PushNotificationService(IRepository<PushNotificationSettings, string> pushNotificationRepository, IFantasoccerLogger logger)
        {
            _pushNotificationRepository = pushNotificationRepository;
            _logger = logger;
        }

        public async ValueTask SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message)
        {
            List<PushSubscription> noLongerValidSubscriptions = new();

            foreach (var notification in notificationSettings.Subscriptions)
            {
                _statistics.TotalAttempted++;
                var subscription = new PushSubscription(notification.Endpoint, notification.P256DH, notification.Auth);
                var vapidDetails = new VapidDetails($"mailto:{email.Split('@').First()}@fanta.zone", PublicKey, PrivateKey);

                bool success = false;
                string? lastError = null;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        using var webPushClient = new WebPushClient();
                        var jsonMessage = message.ToJson();
                        await webPushClient.SendNotificationAsync(subscription, jsonMessage, vapidDetails);
                        
                        _statistics.TotalSucceeded++;
                        success = true;
                        break;
                    }
                    catch (WebPushException wpe)
                    {
                        lastError = wpe.Message;
                        
                        if (IsSubscriptionInvalid(wpe.Message))
                        {
                            noLongerValidSubscriptions.Add(subscription);
                            _statistics.TotalDiscarded++;
                            await _logger.LogAsync<PushNotificationService>($"Subscription no longer valid for {email}. Removed.");
                            break;
                        }

                        if (IsPermanentError(wpe.StatusCode))
                        {
                            _statistics.TotalFailed++;
                            await _logger.LogAsync<PushNotificationService>($"Permanent error for {email} (attempt {attempt}/{MaxRetries}): {wpe.StatusCode} - {wpe.Message}");
                            break;
                        }

                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelayMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;

                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelayMs);
                        }
                    }
                }

                if (!success && !noLongerValidSubscriptions.Contains(subscription))
                {
                    _statistics.TotalFailed++;
                    await _logger.LogAsync<PushNotificationService>($"Failed to send notification to {email} after {MaxRetries} attempts. Last error: {lastError}");
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

        public PushNotificationStatistics GetStatistics()
        {
            return _statistics;
        }

        public void ResetStatistics()
        {
            _statistics.TotalAttempted = 0;
            _statistics.TotalSucceeded = 0;
            _statistics.TotalFailed = 0;
            _statistics.TotalDiscarded = 0;
        }

        private static bool IsSubscriptionInvalid(string message)
        {
            return message.ToLower().Contains("subscription no longer valid") || 
                   message.ToLower().Contains("410");
        }

        private static bool IsPermanentError(System.Net.HttpStatusCode? statusCode)
        {
            return statusCode == System.Net.HttpStatusCode.BadRequest ||
                   statusCode == System.Net.HttpStatusCode.Unauthorized ||
                   statusCode == System.Net.HttpStatusCode.Forbidden ||
                   statusCode == System.Net.HttpStatusCode.NotFound ||
                   statusCode == System.Net.HttpStatusCode.Gone;
        }
    }
}
