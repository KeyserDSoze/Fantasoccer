using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Api.Mcp;

// Uses exactly the same ASP.NET Core ticket protector as the frontend login.
// OAuth metadata on the ticket additionally binds it to MCP and its stored grant.
public sealed class McpBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    McpOptions settings,
    McpTokenValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var ticket = bearerOptions.Get(BearerTokenDefaults.AuthenticationScheme)
            .BearerTokenProtector.Unprotect(header[7..]);
        if (ticket?.Properties.ExpiresUtc is not { } expiry || expiry <= TimeProvider.GetUtcNow())
            return AuthenticateResult.Fail("Invalid or expired bearer token.");

        var principal = await validator.ValidateAsync(ticket, Context.RequestAborted);
        return principal == null ? AuthenticateResult.Fail("Invalid or revoked MCP connection.") :
            AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{settings.Issuer.TrimEnd('/')}/.well-known/oauth-protected-resource/mcp\", scope=\"{McpOptions.Scope}\"";
        return Task.CompletedTask;
    }
}
