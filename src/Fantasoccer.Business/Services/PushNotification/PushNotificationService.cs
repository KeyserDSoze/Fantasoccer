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
        private readonly PushNotificationSettingsService _settings;
        private readonly IFantasoccerLogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private int _attempted, _succeeded, _failed, _discarded;

        public PushNotificationService(PushNotificationSettingsService settings, IFantasoccerLogger logger, IHttpClientFactory httpClientFactory)
        {
            _settings = settings;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async ValueTask<PushNotificationDeliveryResult> SendNotificationAsync(string email, PushNotificationSettings notificationSettings, Notification message, CancellationToken cancellationToken = default)
        {
            var result = new PushNotificationDeliveryResult();
            var latest = await _settings.GetAsync(email, cancellationToken);
            var subscriptions = (notificationSettings.Subscriptions ?? [])
                .Where(s => latest.Subscriptions.Any(current => current.Endpoint == s.Endpoint && current.P256DH == s.P256DH && current.Auth == s.Auth))
                .DistinctBy(s => s.Endpoint).Select(s => new PushSubscription(s.Endpoint, s.P256DH, s.Auth)).ToArray();
            foreach (var subscription in subscriptions)
            {
                Interlocked.Increment(ref _attempted);
                if (!PushSubscriptionValidator.IsValid(subscription))
                {
                    await _settings.RemoveInvalidAsync(email, subscription, cancellationToken);
                    Interlocked.Increment(ref _discarded);
                    result.CompletedEndpoints.Add(subscription.Endpoint);
                    continue;
                }
                var vapidDetails = new VapidDetails($"mailto:{email.Split('@').First()}@fanta.zone", PublicKey, PrivateKey);

                bool success = false;
                string? lastError = null;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        using var webPushClient = new WebPushClient(_httpClientFactory.CreateClient("web-push"));
                        var payload = JsonSerializer.Deserialize<Notification>(JsonSerializer.Serialize(message))!;
                        payload.Data ??= new NotificationData();
                        payload.Data.RecipientEmail = email;
                        var jsonMessage = payload.ToJson();
                        await webPushClient.SendNotificationAsync(subscription, jsonMessage, vapidDetails, cancellationToken: cancellationToken);
                        
                        Interlocked.Increment(ref _succeeded);
                        result.CompletedEndpoints.Add(subscription.Endpoint);
                        success = true;
                        break;
                    }
                    catch (WebPushException wpe)
                    {
                        lastError = wpe.Message;
                        
                        if (wpe.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
                        {
                            await _settings.RemoveInvalidAsync(email, subscription, cancellationToken);
                            Interlocked.Increment(ref _discarded);
                            result.CompletedEndpoints.Add(subscription.Endpoint);
                            success = true;
                            await _logger.LogAsync<PushNotificationService>($"Subscription no longer valid for {email}. Removed.");
                            break;
                        }

                        if (IsPermanentError(wpe.StatusCode))
                        {
                            await _logger.LogAsync<PushNotificationService>($"Permanent error for {email} (attempt {attempt}/{MaxRetries}): {wpe.StatusCode} - {wpe.Message}");
                            break;
                        }

                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelayMs, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;

                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelayMs, cancellationToken);
                        }
                    }
                }

                if (!success)
                {
                    Interlocked.Increment(ref _failed);
                    result.RetryEndpoints.Add(subscription.Endpoint);
                    await _logger.LogAsync<PushNotificationService>($"Push delivery to {email} needs a deferred retry. Last error: {lastError}");
                }
            }

            return result;
        }

        public PushNotificationStatistics GetStatistics()
        {
            return new() { TotalAttempted = Volatile.Read(ref _attempted), TotalSucceeded = Volatile.Read(ref _succeeded),
                TotalFailed = Volatile.Read(ref _failed), TotalDiscarded = Volatile.Read(ref _discarded) };
        }

        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _attempted, 0);
            Interlocked.Exchange(ref _succeeded, 0);
            Interlocked.Exchange(ref _failed, 0);
            Interlocked.Exchange(ref _discarded, 0);
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
