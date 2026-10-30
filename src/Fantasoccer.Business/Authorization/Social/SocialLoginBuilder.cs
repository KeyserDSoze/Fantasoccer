namespace Fantasoccer.Authentication;

// Settings of one provider: application (client) id and the frontend origins allowed to start a login.
public sealed class SocialProviderSettings
{
    private readonly List<Uri> _allowedDomains = [];

    public string? ClientId { get; set; }
    // Kept for providers that need a confidential client; the Microsoft SPA flow (PKCE) does not send it.
    public string? ClientSecret { get; set; }
    public bool IsActive => !string.IsNullOrWhiteSpace(ClientId) && _allowedDomains.Count > 0;

    public SocialProviderSettings AddUri(string uri)
    {
        uri = uri.Trim();
        if (uri.Length == 0) return this;
        if (!uri.Contains("http", StringComparison.OrdinalIgnoreCase)) uri = "https://" + uri;
        _allowedDomains.Add(new Uri(uri));
        return this;
    }

    public SocialProviderSettings AddUris(params string[] uris)
    {
        foreach (var uri in uris) AddUri(uri);
        return this;
    }

    // The allowed origin matching the caller (scheme, host and port), or the first one without a caller.
    public string? CheckDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return _allowedDomains.FirstOrDefault()?.AbsoluteUri;
        if (!Uri.TryCreate(domain.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? domain : "https://" + domain, UriKind.Absolute, out var candidate)) return null;
        return _allowedDomains.FirstOrDefault(x => x.Port == candidate.Port && x.Scheme == candidate.Scheme && x.Host == candidate.Host)?.AbsoluteUri;
    }
}

public sealed class SocialLoginBuilder
{
    public SocialProviderSettings Microsoft { get; } = new();
    // Reserved for the Google login to come; not exchanged yet.
    public SocialProviderSettings Google { get; } = new();
}
