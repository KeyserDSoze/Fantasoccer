using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Fantasoccer.Authentication;

public static class SocialClaimTypes
{
    // Frontend origin the token was issued to; a refresh is accepted only from the same origin.
    public const string Domain = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/domain";
}

public sealed record SocialExchangeRequest(string? Domain, string? RedirectPath, IReadOnlyDictionary<string, string>? Parameters)
{
    public string RedirectUri => string.IsNullOrWhiteSpace(Domain) ? RedirectPath ?? string.Empty : Domain.TrimEnd('/') + RedirectPath;
    public string? Parameter(string key) => Parameters is not null && Parameters.TryGetValue(key, out var value) ? value : null;
}

// Error is a message safe to return to the client (never provider payloads or secrets).
public sealed record SocialExchangeResult(TokenResponse? Token, string? Error)
{
    public static SocialExchangeResult Success(TokenResponse token) => new(token, null);
    public static SocialExchangeResult Failure(string error) => new(null, error);
    public static SocialExchangeResult Rejected { get; } = new(null, null);
}

// Turns what the frontend sends (an authorization code, or our refresh token) into a verified identity.
public interface ISocialCodeExchanger
{
    Task<SocialExchangeResult> ExchangeAsync(string code, SocialExchangeRequest request, CancellationToken cancellationToken);
}

// Public keys used to verify provider ID tokens, cached and refreshed by the metadata manager.
public interface ISocialSigningKeyProvider
{
    Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(string metadataAddress, CancellationToken cancellationToken);
}

internal sealed class OpenIdSigningKeyProvider : ISocialSigningKeyProvider
{
    private readonly Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = [];

    public async Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(string metadataAddress, CancellationToken cancellationToken)
    {
        ConfigurationManager<OpenIdConnectConfiguration> manager;
        lock (_managers)
        {
            if (!_managers.TryGetValue(metadataAddress, out manager!))
                _managers[metadataAddress] = manager = new(metadataAddress, new OpenIdConnectConfigurationRetriever());
        }
        return (await manager.GetConfigurationAsync(cancellationToken)).SigningKeys.ToArray();
    }
}

// Authorization-code (PKCE) exchange with Microsoft personal accounts. The ID token must be signed by
// Microsoft, unexpired, issued by the consumers tenant and addressed to this application.
internal sealed class MicrosoftCodeExchanger(IHttpClientFactory clients, SocialLoginBuilder login, ISocialSigningKeyProvider keys) : ISocialCodeExchanger
{
    public const string HttpClientName = "MicrosoftAuthenticationClient";
    public const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    public const string MetadataAddress = "https://login.microsoftonline.com/consumers/v2.0/.well-known/openid-configuration";
    public const string Issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
    // RFC 7636: the client generates a fresh verifier per login. There is deliberately no server-side default:
    // a public constant would make the code challenge worthless for any client that forgot to send its own.

    private sealed class ProviderTokens
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
    }

    public async Task<SocialExchangeResult> ExchangeAsync(string code, SocialExchangeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return SocialExchangeResult.Rejected;
        var settings = login.Microsoft;
        if (!settings.IsActive) return SocialExchangeResult.Rejected;
        var origin = settings.CheckDomain(request.Domain);
        if (origin is null) return SocialExchangeResult.Failure("Invalid domain");
        var verifier = request.Parameter("code_verifier");
        if (verifier is null || verifier.Length is < 43 or > 128 || verifier.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.' or '_' or '~')))
            return SocialExchangeResult.Failure("A PKCE code_verifier is required.");
        var client = clients.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = settings.ClientId!,
            ["redirect_uri"] = request.RedirectUri,
            ["code_verifier"] = verifier,
            ["grant_type"] = "authorization_code",
            ["scope"] = "profile openid email"
        });
        using var response = await client.PostAsync(string.Empty, content, cancellationToken);
        if (!response.IsSuccessStatusCode) return SocialExchangeResult.Failure("Token exchange failed.");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokens = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<ProviderTokens>(body);
        if (tokens?.AccessToken is null || tokens.IdToken is null) return SocialExchangeResult.Failure("Invalid token response.");
        try
        {
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Issuer,
                ValidateAudience = true, ValidAudience = settings.ClientId,
                ValidateIssuerSigningKey = true, ValidateLifetime = true,
                IssuerSigningKeys = await keys.GetKeysAsync(MetadataAddress, cancellationToken)
            };
            new JwtSecurityTokenHandler().ValidateToken(tokens.IdToken, validation, out var validated);
            var claims = ((JwtSecurityToken)validated).Claims.ToList();
            var username = claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value ?? claims.FirstOrDefault(x => x.Type == "email")?.Value;
            return string.IsNullOrWhiteSpace(username) ? SocialExchangeResult.Failure("The identity has no email.") : SocialExchangeResult.Success(new() { Username = username, Claims = claims });
        }
        catch (SecurityTokenException)
        {
            return SocialExchangeResult.Failure("Token validation failed.");
        }
    }
}

// Refresh: the code is the refresh token issued by this application (a protected ticket). It is valid only until
// its own expiry and only from the frontend origin it was issued to.
internal sealed class RefreshTokenExchanger(IOptionsMonitor<BearerTokenOptions> options, TimeProvider clock) : ISocialCodeExchanger
{
    public Task<SocialExchangeResult> ExchangeAsync(string code, SocialExchangeRequest request, CancellationToken cancellationToken)
    {
        var ticket = options.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(code);
        var expires = ticket?.Properties?.ExpiresUtc ?? clock.GetUtcNow().AddHours(1);
        var identity = ticket?.Principal?.Identity;
        if (identity is { IsAuthenticated: true, Name: not null } && clock.GetUtcNow() < expires &&
            (request.Domain is null || ticket!.Principal!.Claims.Any(x => x.Type == SocialClaimTypes.Domain && x.Value == request.Domain)))
            return Task.FromResult(SocialExchangeResult.Success(new() { Username = identity.Name, Claims = ticket!.Principal!.Claims.ToList() }));
        return Task.FromResult(SocialExchangeResult.Rejected);
    }
}
