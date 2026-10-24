using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Fantasoccer.Application.Api.Mcp;

// Require a valid snapshot AND a successful CAS at redemption, even if the request
// authenticated before another instance consumed the same code/refresh token.
public sealed class McpRedeemTokenHandler(IOpenIddictTokenManager tokens)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var refresh = context.Request.IsRefreshTokenGrantType();
        if (context.EndpointType != OpenIddictServerEndpointType.Token || (!refresh && !context.Request.IsAuthorizationCodeGrantType()))
        {
            await new OpenIddictServerHandlers.RedeemTokenEntry(tokens).HandleAsync(context);
            return;
        }
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var principal = refresh ? authentication?.RefreshTokenPrincipal : authentication?.AuthorizationCodePrincipal;
        var id = principal?.GetTokenId();
        var token = id == null ? null : await tokens.FindByIdAsync(id, context.CancellationToken);
        if (token == null || !await tokens.HasStatusAsync(token, OpenIddictConstants.Statuses.Valid, context.CancellationToken) ||
            !await tokens.TryRedeemAsync(token, context.CancellationToken))
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "Authorization code or refresh token was already used or changed concurrently.");
    }
}
