using System.Net;
using System.Text.Json;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Business;
using Microsoft.Extensions.Options;
using WebPush;

namespace Fantasoccer.Application.Api.Notifications;

public sealed class WebPushDeliveryTransport(IHttpClientFactory clients, IOptions<PushTransportOptions> options) : IPushDeliveryTransport
{
    public async Task<PushTransportOutcome> SendAsync(PushSubscriptionDto device, PushPayload payload, int timeToLiveSeconds, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return PushTransportOutcome.Retry;
        var subscription = new PushSubscription(device.Endpoint, device.P256dh, device.Auth);
        if (!PushSubscriptionValidator.IsValid(subscription)) return PushTransportOutcome.Gone;
        var settings = options.Value;
        using var client = new WebPushClient(clients.CreateClient("sql-web-push"));
        try
        {
            await client.SendNotificationAsync(subscription, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                new Dictionary<string, object> { ["vapidDetails"] = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey), ["TTL"] = Math.Clamp(timeToLiveSeconds, 1, 86400) }, cancellationToken);
            return PushTransportOutcome.ProviderAccepted;
        }
        catch (WebPushException error)
        {
            return error.StatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.Gone => PushTransportOutcome.Gone,
                // A VAPID/key-rotation/configuration failure must not erase the
                // account's subscriptions. Retry boundedly while ops repairs it.
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => PushTransportOutcome.Retry,
                { } status when (int)status >= 500 || (int)status is >= 300 and < 400 => PushTransportOutcome.Retry,
                _ => PushTransportOutcome.Rejected
            };
        }
    }
}
