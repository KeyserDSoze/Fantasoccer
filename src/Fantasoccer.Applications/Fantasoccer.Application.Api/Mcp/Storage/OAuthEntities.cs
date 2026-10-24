using System.Collections.Immutable;
using System.Text.Json;

namespace Fantasoccer.Application.Api.Mcp.Storage;

// Shared persistence shape for OpenIddict's four entity contracts. Separate types
// keep store resolution, keys and lifecycle operations isolated.
public abstract class OAuthEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Version { get; set; }
    public string? ApplicationId { get; set; }
    public string? AuthorizationId { get; set; }
    public string? ApplicationType { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ClientType { get; set; }
    public string? ConsentType { get; set; }
    public string? DisplayName { get; set; }
    public ImmutableDictionary<string, string> DisplayNames { get; set; } = ImmutableDictionary<string, string>.Empty;
    public ImmutableDictionary<string, string> Descriptions { get; set; } = ImmutableDictionary<string, string>.Empty;
    public string? Description { get; set; }
    public string? Name { get; set; }
    public string? JsonWebKeySet { get; set; }
    public ImmutableArray<string> Permissions { get; set; } = [];
    public ImmutableArray<string> PostLogoutRedirectUris { get; set; } = [];
    public ImmutableArray<string> RedirectUris { get; set; } = [];
    public ImmutableArray<string> Requirements { get; set; } = [];
    public ImmutableArray<string> Resources { get; set; } = [];
    public ImmutableArray<string> Scopes { get; set; } = [];
    public ImmutableDictionary<string, string> Settings { get; set; } = ImmutableDictionary<string, string>.Empty;
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
    public string? Subject { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Payload { get; set; }
    public string? ReferenceId { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
    public DateTimeOffset? RedemptionDate { get; set; }
}

public sealed class OAuthApplication : OAuthEntity;
public sealed class OAuthAuthorization : OAuthEntity;
public sealed class OAuthScope : OAuthEntity;
public sealed class OAuthToken : OAuthEntity;
