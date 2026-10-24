namespace Fantasoccer.Application.Api.Mcp;

public sealed class McpOptions
{
    public bool Enabled { get; set; }
    public bool PublishAllTools { get; set; }
    public string Issuer { get; set; } = "";
    public string FrontendUrl { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string CertificateBase64 { get; set; } = "";
    public string? CertificatePassword { get; set; }
    public string DataProtectionKeyPath { get; set; } = "";
    public string DataProtectionBlobName { get; set; } = "";
    public List<McpClientOptions> Clients { get; set; } = [];
    public string Resource => Issuer.TrimEnd('/') + "/mcp";
    public const string Scope = "fantasoccer.full_access";
    public const string Scheme = "McpBearer";
    public const string ConsentScheme = "McpConsent";
    public const string ResourceClaim = "fantasoccer:mcp:resource";
}

public sealed class McpClientOptions
{
    public string ClientId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? ClientSecret { get; set; }
    public List<string> RedirectUris { get; set; } = [];
}
