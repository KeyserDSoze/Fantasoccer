using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fantasoccer.Application.Api.Mcp;

// RFC 7591 open registration. A registration identifies a client, never an app
// user: Microsoft login, explicit consent and S256 PKCE are still mandatory.
public static class McpClientRegistration
{
    private const int MaxBodyBytes = 16 * 1024;

    public static async Task<IResult> RegisterAsync(HttpContext context, McpOptions settings,
        IOpenIddictApplicationManager applications)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!context.Request.HasJsonContentType())
            return Error("invalid_client_metadata", "Send a JSON registration request.");
        // Bound reads even when Content-Length is absent (chunked requests).
        var bytes = new byte[MaxBodyBytes + 1];
        var length = await context.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, context.RequestAborted);
        if (length > MaxBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        JsonDocument document;
        try { document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException) { return Error("invalid_client_metadata", "Invalid JSON metadata."); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(x => x.Name).Distinct().Count() != root.EnumerateObject().Count())
                return Error("invalid_client_metadata", "Expected one metadata object without duplicate fields.");
            if (!Strings(root, "redirect_uris", out var redirects) || redirects.Length is < 1 or > 10 ||
                redirects.Distinct(StringComparer.Ordinal).Count() != redirects.Length || redirects.Any(x => !IsValidRedirect(x)))
                return Error("invalid_redirect_uri", "Provide 1-10 exact HTTPS or loopback HTTP redirect URIs, without credentials, fragments or wildcards.");
            if (!OptionalString(root, "client_name", out var name) || name?.Length > 120 || name?.Any(char.IsControl) == true)
                return Error("invalid_client_metadata", "client_name must be text up to 120 characters without control characters.");
            if (!OptionalString(root, "token_endpoint_auth_method", out var method))
                return Error("invalid_client_metadata", "Invalid token_endpoint_auth_method.");
            method ??= "client_secret_basic"; // RFC 7591 default; native clients should request 'none'.
            if (method is not ("none" or "client_secret_basic" or "client_secret_post"))
                return Error("invalid_client_metadata", "Supported authentication methods: none, client_secret_basic, client_secret_post.");
            if (root.TryGetProperty("grant_types", out _) &&
                (!Strings(root, "grant_types", out var grants) || !grants.Contains("authorization_code") ||
                 grants.Any(x => x is not ("authorization_code" or "refresh_token"))))
                return Error("invalid_client_metadata", "Only authorization_code and refresh_token are supported.");
            if (root.TryGetProperty("response_types", out _) &&
                (!Strings(root, "response_types", out var responses) || responses.Length != 1 || responses[0] != "code"))
                return Error("invalid_client_metadata", "Only response_type code is supported.");
            if (!OptionalString(root, "scope", out var scope) || scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(x => x is not (McpOptions.Scope or Scopes.OfflineAccess)) == true)
                return Error("invalid_client_metadata", "Unsupported scope.");

            var clientId = "mcp-" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var secret = method == "none" ? null : WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId, ClientSecret = secret,
                DisplayName = string.IsNullOrWhiteSpace(name) ? "Client MCP" : name.Trim(),
                ClientType = secret == null ? ClientTypes.Public : ClientTypes.Confidential,
                ConsentType = ConsentTypes.Explicit
            };
            foreach (var redirect in redirects) descriptor.RedirectUris.Add(new Uri(redirect));
            var grantTypes = root.TryGetProperty("grant_types", out _) ? root.GetProperty("grant_types").EnumerateArray().Select(x => x.GetString()!).ToArray() : ["authorization_code"];
            descriptor.Permissions.UnionWith([
                Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + McpOptions.Scope, Permissions.Prefixes.Resource + settings.Resource]);
            if (grantTypes.Contains("refresh_token")) descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
            // Never trust caller-supplied IDs, secrets, permissions or remote metadata.
            // OpenIddict hashes confidential secrets before persisting in Blob storage.
            await applications.CreateAsync(descriptor, context.RequestAborted);
            var response = new Dictionary<string, object?>
            {
                ["client_id"] = clientId, ["client_id_issued_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["client_name"] = descriptor.DisplayName, ["redirect_uris"] = redirects,
                ["token_endpoint_auth_method"] = method, ["grant_types"] = grantTypes,
                ["response_types"] = new[] { "code" }, ["scope"] = scope ?? McpOptions.Scope + " " + Scopes.OfflineAccess
            };
            if (secret != null)
            {
                response["client_secret"] = secret;
                response["client_secret_expires_at"] = 0;
            }
            return Results.Json(response, statusCode: StatusCodes.Status201Created);
        }
    }

    public static bool IsValidRedirect(string value)
        => value.Length <= 2048 && value == value.Trim() && !value.Any(char.IsControl) && !value.Contains('*') && !value.Contains('\\') &&
           Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) &&
           string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && !value.Contains('#') &&
           (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));

    private static bool OptionalString(JsonElement root, string key, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(key, out var property)) return true;
        if (property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString();
        return true;
    }

    private static bool Strings(JsonElement root, string key, out string[] values)
    {
        values = [];
        if (!root.TryGetProperty(key, out var property) || property.ValueKind != JsonValueKind.Array ||
            property.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) return false;
        values = property.EnumerateArray().Select(x => x.GetString()!).ToArray();
        return true;
    }

    private static IResult Error(string error, string description)
        => Results.BadRequest(new { error, error_description = description });
}
