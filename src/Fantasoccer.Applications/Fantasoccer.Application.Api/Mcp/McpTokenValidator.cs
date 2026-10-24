using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using RepositoryFramework;
using Rystem.Authentication.Social;

namespace Fantasoccer.Application.Api.Mcp;

public sealed class McpTokenValidator(McpOptions settings, IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations, IOpenIddictApplicationManager applications,
    IRepository<AppIdentity, string> users, ISocialUserProvider socialUsers)
{
    public async Task<ClaimsPrincipal?> ValidateAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        var principal = ticket.Principal;
        if (ticket.Properties.ExpiresUtc is not { } expiry || expiry <= DateTimeOffset.UtcNow ||
            principal.FindFirstValue(McpOptions.ResourceClaim) != settings.Resource ||
            !principal.GetAudiences().Contains(settings.Resource) ||
            principal.GetTokenType() != OpenIddictConstants.TokenTypeIdentifiers.AccessToken ||
            !principal.HasScope(McpOptions.Scope) || principal.GetClaim(OpenIddictConstants.Claims.Issuer) != settings.Issuer.TrimEnd('/')) return null;

        var tokenId = principal.GetTokenId();
        var grantId = principal.GetAuthorizationId();
        var token = tokenId == null ? null : await tokens.FindByIdAsync(tokenId, cancellationToken);
        var grant = grantId == null ? null : await authorizations.FindByIdAsync(grantId, cancellationToken);
        if (token == null || grant == null ||
            !await tokens.HasStatusAsync(token, OpenIddictConstants.Statuses.Valid, cancellationToken) ||
            !await authorizations.HasStatusAsync(grant, OpenIddictConstants.Statuses.Valid, cancellationToken)) return null;
        var applicationId = await tokens.GetApplicationIdAsync(token, cancellationToken);
        var application = applicationId == null ? null : await applications.FindByIdAsync(applicationId, cancellationToken);
        var clientId = application == null ? null : await applications.GetClientIdAsync(application, cancellationToken);
        if (!settings.Clients.Any(x => x.ClientId == clientId)) return null;

        var subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (subject == null || await users.GetAsync(subject, cancellationToken) == null) return null;
        var identity = new ClaimsIdentity("BearerToken", ClaimTypes.Name, ClaimTypes.Role);
        // Re-read application claims: demotion takes effect without waiting for token expiry.
        await foreach (var claim in socialUsers.GetClaimsAsync(new TokenResponse { Username = subject, Claims = [] }, cancellationToken))
            identity.AddClaim(claim);
        identity.AddClaim(new Claim(McpOptions.ResourceClaim, settings.Resource));
        return new ClaimsPrincipal(identity);
    }
}
