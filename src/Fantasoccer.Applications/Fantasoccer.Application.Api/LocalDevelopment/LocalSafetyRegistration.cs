namespace Fantasoccer.Application.Api.LocalDevelopment;

public static class LocalSafetyRegistration
{
    public static void AddLocalSafetyControls(this WebApplicationBuilder builder)
    {
        // These controls are not authentication bypasses. Microsoft login remains real.
        if (!builder.Configuration.GetValue("ExternalHttp:Enabled", true))
        {
            foreach (var name in new[] { "base", "player-images", "seriea", "all-players", "seriea-gazzetta-calendar" })
                builder.Services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new DisabledExternalHttpHandler());
        }
        if (!builder.Configuration.GetValue("Push:Enabled", true))
            builder.Services.AddTransient<IPushNotificationService, DisabledPushNotificationService>();
    }
}

internal sealed class DisabledExternalHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("External data fetching is disabled in this environment.");
}

internal sealed class DisabledPushNotificationService : IPushNotificationService
{
    // Do not mark real subscriptions as delivered or remove them on a copied dataset.
    public ValueTask<PushNotificationDeliveryResult> SendNotificationAsync(string email, PushNotificationSettings settings,
        Notification message, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Push delivery is disabled in this environment.");
    public PushNotificationStatistics GetStatistics() => new();
    public void ResetStatistics() { }
}
