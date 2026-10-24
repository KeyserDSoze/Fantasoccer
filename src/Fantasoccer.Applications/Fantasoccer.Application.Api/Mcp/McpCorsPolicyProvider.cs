using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Fantasoccer.Application.Api.Mcp;

// Browser-based MCP clients use bearer headers, never frontend/consent cookies.
// Keep the existing credentialed SPA CORS policy on approval and legacy APIs.
public sealed class McpCorsPolicyProvider(IOptions<CorsOptions> options, McpOptions settings,
    IOpenIddictApplicationManager applications) : ICorsPolicyProvider
{
    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var path = context.Request.Path;
        if (!(path.StartsWithSegments("/mcp") || path == "/connect/register" || path == "/connect/token" ||
              path == "/connect/revoke" || path.StartsWithSegments("/.well-known")))
            return options.Value.GetPolicy(policyName ?? options.Value.DefaultPolicyName);

        var origin = context.Request.Headers.Origin.ToString();
        var allowed = origin == new Uri(settings.FrontendUrl).GetLeftPart(UriPartial.Authority) ||
                      origin == new Uri(settings.Issuer).GetLeftPart(UriPartial.Authority);
        if (path == "/connect/register" || path.StartsWithSegments("/.well-known"))
            allowed |= McpClientRegistration.IsValidRedirect(origin) &&
                       new Uri(origin).GetLeftPart(UriPartial.Authority) == origin;
        else if (!allowed && origin.Length > 0)
        {
            await foreach (var client in applications.ListAsync(cancellationToken: context.RequestAborted))
            {
                if ((await applications.GetRedirectUrisAsync(client, context.RequestAborted))
                    .Any(x => new Uri(x).GetLeftPart(UriPartial.Authority) == origin))
                {
                    allowed = true;
                    break;
                }
            }
        }
        var builder = new CorsPolicyBuilder().AllowAnyHeader().AllowAnyMethod()
            .WithExposedHeaders("WWW-Authenticate", "MCP-Session-Id", "MCP-Protocol-Version");
        if (allowed) builder.WithOrigins(origin);
        return builder.Build();
    }
}
