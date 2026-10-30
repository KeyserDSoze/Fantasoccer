namespace Fantasoccer;

// Private infrastructure documents. Never expose these through an API.
public sealed class OAuthDocument
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Payload { get; set; }
    public DateTimeOffset? DeleteAfterUtc { get; set; }
}

public interface IOAuthDocumentStorage
{
    Task<OAuthDocument?> GetAsync(string id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OAuthDocument> ListAsync(string kind, CancellationToken cancellationToken = default);
    // null expectedVersion means insert-only; otherwise compare-and-swap.
    Task<bool> WriteAsync(OAuthDocument document, string? expectedVersion, CancellationToken cancellationToken = default);
    Task<long> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
