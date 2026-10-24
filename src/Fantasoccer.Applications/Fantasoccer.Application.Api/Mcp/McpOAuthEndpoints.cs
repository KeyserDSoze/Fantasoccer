using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using RepositoryFramework;
using Rystem.Authentication.Social;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fantasoccer.Application.Api.Mcp;

public static class McpOAuthEndpoints
{
    private const string TransactionCookie = "__Host-Fantasoccer-McpTransaction";
    private const string RequestBinding = "mcp-request-binding";
    public sealed record Approval(string TransactionId, bool Approved);
    public sealed record Disconnect(string ClientId);

    public static void MapMcpOAuthEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<McpOptions>();
        app.MapGet("/.well-known/oauth-protected-resource/mcp", () => Results.Json(new
        {
            resource = settings.Resource,
            authorization_servers = new[] { new Uri(settings.Issuer).AbsoluteUri },
            scopes_supported = new[] { McpOptions.Scope },
            bearer_methods_supported = new[] { "header" }
        }));
        app.MapGet("/.well-known/oauth-protected-resource", () => Results.Redirect("/.well-known/oauth-protected-resource/mcp"));

        app.MapGet("/connect/authorize", AuthorizeAsync).RequireRateLimiting("mcp-oauth");
        app.MapPost("/connect/token", TokenAsync).RequireRateLimiting("mcp-oauth");
        app.MapPost("/connect/register", McpClientRegistration.RegisterAsync).RequireRateLimiting("mcp-registration");
        app.MapGet("/connect/request/{id}", async (string id, McpConsentTransactions transactions, HttpContext context) =>
        {
            NoStore(context);
            var transaction = await transactions.GetAsync(id, context.RequestAborted);
            return transaction == null ? Results.NotFound() : Results.Json(new
            {
                clientName = transaction.ClientName,
                scope = McpOptions.Scope,
                description = "Accesso a tutte le funzioni consentite dal tuo account, incluse modifiche e operazioni amministrative se autorizzate."
            });
        }).RequireRateLimiting("mcp-oauth");
        app.MapPost("/connect/approve", ApproveAsync).RequireRateLimiting("mcp-oauth");
        app.MapGet("/connect/complete", CompleteAsync).RequireRateLimiting("mcp-oauth");
        app.MapPost("/connect/disconnect", DisconnectAsync).RequireRateLimiting("mcp-oauth");
        app.MapGet("/connect/connections", ConnectionsAsync).RequireRateLimiting("mcp-oauth");
    }

    private static async Task<IResult> AuthorizeAsync(HttpContext context, McpOptions settings,
        McpConsentTransactions transactions, IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations, IRepository<AppIdentity, string> users,
        ISocialUserProvider socialUsers)
    {
        NoStore(context);
        var request = context.GetOpenIddictServerRequest()!;
        // OpenIddict has already validated the client and its exact redirect URI.
        if (!ValidResource(request, settings) || !request.HasScope(McpOptions.Scope))
            return OAuthError(Errors.InvalidTarget, "Request this MCP resource and its full_access scope.");

        var consent = await context.AuthenticateAsync(McpOptions.ConsentScheme);
        var binding = Bind(context.Request.QueryString.Value ?? "");
        if (!consent.Succeeded || consent.Properties?.Items.TryGetValue(RequestBinding, out var savedBinding) != true || savedBinding != binding)
        {
            var client = await applications.FindByClientIdAsync(request.ClientId!) ?? throw new InvalidOperationException();
            var id = await transactions.CreateAsync("/connect/authorize" + context.Request.QueryString,
                await applications.GetDisplayNameAsync(client) ?? request.ClientId!, context.RequestAborted);
            context.Response.Cookies.Append(TransactionCookie, id, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromMinutes(10)
            });
            return Results.Redirect(QueryHelpers.AddQueryString(settings.FrontendUrl, "mcp_request", id));
        }

        await context.SignOutAsync(McpOptions.ConsentScheme);
        var consentId = consent.Principal!.FindFirstValue("mcp-consent-id");
        if (consentId == null || await transactions.RedeemAsync(consentId, context.RequestAborted) == null)
            return OAuthError(Errors.AccessDenied, "Consent already used or expired.");
        var email = consent.Principal!.FindFirstValue(ClaimTypes.Email);
        if (email == null || await users.GetAsync(email, context.RequestAborted) == null)
            return OAuthError(Errors.AccessDenied, "Application account unavailable.");
        var principal = await BuildPrincipalAsync(email, socialUsers, context.RequestAborted);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(settings.Resource);
        var application = await applications.FindByClientIdAsync(request.ClientId!) ?? throw new InvalidOperationException();
        var grant = await authorizations.CreateAsync(principal, email,
            (await applications.GetIdAsync(application))!, AuthorizationTypes.Permanent, principal.GetScopes(), context.RequestAborted);
        principal.SetAuthorizationId(await authorizations.GetIdAsync(grant));
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> ApproveAsync(Approval approval, HttpContext context, McpOptions settings,
        McpConsentTransactions transactions, IRepository<AppIdentity, string> users)
    {
        NoStore(context);
        // The SPA authenticates this POST with its EXISTING frontend token, not a new Microsoft flow.
        var login = await context.AuthenticateAsync(BearerTokenDefaults.AuthenticationScheme);
        var email = login.Principal?.FindFirstValue(ClaimTypes.Email);
        if (!login.Succeeded || email == null || login.Principal!.HasClaim(x => x.Type == McpOptions.ResourceClaim))
            return Results.Unauthorized();
        if (await users.GetAsync(email, context.RequestAborted) == null) return Results.Forbid();
        var origin = context.Request.Headers.Origin.ToString();
        if (origin != new Uri(settings.FrontendUrl).GetLeftPart(UriPartial.Authority)) return Results.StatusCode(403);
        var transaction = await transactions.GetAsync(approval.TransactionId, context.RequestAborted);
        if (transaction == null) return Results.BadRequest(new { error = "Connection expired. Start again from your MCP client." });
        if (!approval.Approved)
        {
            await transactions.DenyAsync(approval.TransactionId, context.RequestAborted);
            return Results.Json(new { redirectUrl = settings.FrontendUrl });
        }
        if (await transactions.ApproveAsync(approval.TransactionId, email, context.RequestAborted) == null)
            return Results.BadRequest(new { error = "Already approved or expired." });
        return Results.Json(new { redirectUrl = settings.Issuer.TrimEnd('/') + "/connect/complete?id=" + approval.TransactionId });
    }

    private static async Task<IResult> CompleteAsync(string id, HttpContext context, McpConsentTransactions transactions)
    {
        NoStore(context);
        // Top-level navigation sends this first-party Lax cookie even when the SPA/API have different sites.
        // Cross-site fetch never needs third-party cookies, and approvals cannot be completed in another browser.
        if (context.Request.Cookies[TransactionCookie] != id) return Results.StatusCode(403);
        var transaction = await transactions.ConsumeAsync(id, context.RequestAborted);
        if (transaction?.Email == null) return Results.BadRequest();
        context.Response.Cookies.Delete(TransactionCookie, new CookieOptions { Path = "/", Secure = true });
        var query = transaction.ReturnUrl["/connect/authorize".Length..];
        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(2) };
        properties.Items[RequestBinding] = Bind(query);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, transaction.Email), new Claim("mcp-consent-id", id)], McpOptions.ConsentScheme);
        await context.SignInAsync(McpOptions.ConsentScheme, new ClaimsPrincipal(identity), properties);
        return Results.Redirect(transaction.ReturnUrl);
    }

    private static async Task<IResult> TokenAsync(HttpContext context, McpOptions settings,
        IRepository<AppIdentity, string> users, ISocialUserProvider socialUsers)
    {
        NoStore(context);
        var request = context.GetOpenIddictServerRequest()!;
        if (!ValidResource(request, settings)) return OAuthError(Errors.InvalidTarget, "Invalid MCP resource.");
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var email = result.Principal?.GetClaim(Claims.Subject);
        if (!result.Succeeded || email == null || await users.GetAsync(email, context.RequestAborted) == null)
            return OAuthError(Errors.InvalidGrant, "Application account unavailable.");
        var principal = await BuildPrincipalAsync(email, socialUsers, context.RequestAborted);
        principal.SetScopes(result.Principal!.GetScopes());
        principal.SetResources(settings.Resource);
        principal.SetAuthorizationId(result.Principal.GetAuthorizationId());
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> DisconnectAsync(Disconnect request, HttpContext context,
        IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations)
    {
        var login = await context.AuthenticateAsync(BearerTokenDefaults.AuthenticationScheme);
        var email = login.Principal?.FindFirstValue(ClaimTypes.Email);
        if (!login.Succeeded || email == null || login.Principal!.HasClaim(x => x.Type == McpOptions.ResourceClaim)) return Results.Unauthorized();
        var client = await applications.FindByClientIdAsync(request.ClientId);
        if (client == null) return Results.NoContent();
        await foreach (var grant in authorizations.FindAsync(email, await applications.GetIdAsync(client), Statuses.Valid, AuthorizationTypes.Permanent, null, context.RequestAborted))
            if (!await authorizations.TryRevokeAsync(grant, context.RequestAborted))
                return Results.Conflict(new { error = "Connection changed concurrently. Reload and try disconnecting again." });
        return Results.NoContent();
    }

    private static async Task<IResult> ConnectionsAsync(HttpContext context,
        IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations)
    {
        NoStore(context);
        var login = await context.AuthenticateAsync(BearerTokenDefaults.AuthenticationScheme);
        var email = login.Principal?.FindFirstValue(ClaimTypes.Email);
        if (!login.Succeeded || email == null || login.Principal!.HasClaim(x => x.Type == McpOptions.ResourceClaim)) return Results.Unauthorized();
        var connections = new List<object>();
        var grants = new Dictionary<string, DateTimeOffset?>();
        await foreach (var grant in authorizations.FindAsync(email, null, Statuses.Valid, AuthorizationTypes.Permanent, null, context.RequestAborted))
        {
            var id = await authorizations.GetApplicationIdAsync(grant, context.RequestAborted);
            if (id == null) continue;
            var date = await authorizations.GetCreationDateAsync(grant, context.RequestAborted);
            if (!grants.TryGetValue(id, out var latest) || latest == null || date > latest) grants[id] = date;
        }
        foreach (var (id, latest) in grants)
        {
            var client = await applications.FindByIdAsync(id, context.RequestAborted);
            if (client == null) continue;
            connections.Add(new { clientId = await applications.GetClientIdAsync(client, context.RequestAborted),
                displayName = await applications.GetDisplayNameAsync(client, context.RequestAborted), lastAuthorizedUtc = latest });
        }
        return Results.Json(connections);
    }

    private static bool ValidResource(OpenIddictRequest request, McpOptions settings)
        => request.GetResources() is var resources && resources.Length == 1 && resources[0] == settings.Resource;

    private static async Task<ClaimsPrincipal> BuildPrincipalAsync(string email, ISocialUserProvider socialUsers, CancellationToken cancellationToken)
    {
        var identity = new ClaimsIdentity("Bearer", ClaimTypes.Name, ClaimTypes.Role);
        await foreach (var claim in socialUsers.GetClaimsAsync(new TokenResponse { Username = email, Claims = [] }, cancellationToken))
            identity.AddClaim(claim);
        identity.AddClaim(new Claim(Claims.Subject, email));
        var principal = new ClaimsPrincipal(identity);
        principal.SetDestinations(_ => [Destinations.AccessToken]);
        return principal;
    }

    private static IResult OAuthError(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static string Bind(string query) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(query)));
    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
