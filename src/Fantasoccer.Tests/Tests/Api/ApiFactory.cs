using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fantasoccer.Tests.Tests.BackgroundJobs;
using Fantasoccer.Tests.Tests.Services;
using Fantasoccer.Infrastructure;

namespace Fantasoccer.Tests.Tests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private static int _nextClient;

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Test-Remote-IP", $"2001:db8::{Interlocked.Increment(ref _nextClient):x}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // This fixture characterizes the retired backend explicitly. The actual
        // application defaults to SQL and must not silently fall back to Blob.
        builder.UseSetting("PostgreSql:Enabled", "false");
        builder.UseSetting("Push:Enabled", "true"); // All provider HTTP is intercepted below.
        var pushKeys = WebPush.VapidHelper.GenerateVapidKeys();
        builder.UseSetting("Push:PublicKey", pushKeys.PublicKey);
        builder.UseSetting("Push:PrivateKey", pushKeys.PrivateKey);
        builder.UseSetting("Push:Subject", "mailto:push-tests@example.test");
        builder.UseSetting("Integrations:Infrastructure", "InMemory");
        builder.UseSetting("ConnectionStrings:AppInsights", "InstrumentationKey=00000000-0000-0000-0000-000000000000");
        builder.UseSetting("Telemetry:Provider", "None");
        builder.UseEnvironment("Testing");
        builder.UseSetting("Mcp:Enabled", "true");
        builder.UseSetting("Mcp:Issuer", "https://localhost");
        builder.UseSetting("Mcp:FrontendUrl", "https://frontend.example/");
        builder.UseSetting("Mcp:Clients:0:ClientId", "chatgpt-test");
        builder.UseSetting("Mcp:Clients:0:DisplayName", "ChatGPT test");
        builder.UseSetting("Mcp:Clients:0:RedirectUris:0", "https://chatgpt.com/test-mcp-callback");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientAddressFilter>();
            // Opt-in integration run against Azurite/a dedicated test account, never production.
            var blob = Environment.GetEnvironmentVariable("FANTASOCCER_TEST_OAUTH_BLOB");
            if (!string.IsNullOrEmpty(blob)) services.AddOAuthBlobStorageAsync(blob).GetAwaiter().GetResult();
            services.AddSingleton<TestPlayerImagesHandler>();
            services.AddSingleton<TestWebPushHandler>();
            services.AddHttpClient("web-push")
                .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestWebPushHandler>());
            services.AddHttpClient("player-images")
                .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestPlayerImagesHandler>());
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.Scheme, _ => { });
        });
    }

}

// Give each browser a distinct source IP while exercising the real production
// rate limiters. Only the test host trusts this synthetic header.
internal sealed class TestClientAddressFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, continuation) =>
        {
            if (IPAddress.TryParse(context.Request.Headers["X-Test-Remote-IP"].ToString(), out var address))
                context.Connection.RemoteIpAddress = address;
            await continuation(context);
        });
        next(app);
    };
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "TestAuthentication";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-User"))
            return Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? Context.AuthenticateAsync(BearerTokenDefaults.AuthenticationScheme)
                : Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, Request.Headers["X-Test-User"]!.ToString()),
            new Claim(ClaimTypes.Name, Request.Headers["X-Test-User"]!.ToString())
        };
        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            claims = [.. claims, new Claim(ClaimTypes.Role, role.ToString())];
        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
