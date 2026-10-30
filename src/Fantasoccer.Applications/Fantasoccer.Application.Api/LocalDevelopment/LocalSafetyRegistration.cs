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
    }
}

internal sealed class DisabledExternalHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("External data fetching is disabled in this environment.");
}
