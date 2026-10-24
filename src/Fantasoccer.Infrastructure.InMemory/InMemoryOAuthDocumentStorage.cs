using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Fantasoccer.InMemory;

// Same optimistic concurrency contract as Blob; snapshots must not share mutable instances.
public sealed class InMemoryOAuthDocumentStorage : IOAuthDocumentStorage
{
    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);
    public Task<OAuthDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_documents.TryGetValue(id, out var value) ? JsonSerializer.Deserialize<OAuthDocument>(value) : null);
    }

    public async IAsyncEnumerable<OAuthDocument> ListAsync(string kind,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var value in _documents.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = JsonSerializer.Deserialize<OAuthDocument>(value)!;
            if (document.Kind == kind && document.Payload != null) yield return document;
        }
        await Task.CompletedTask;
    }

    public Task<bool> WriteAsync(OAuthDocument document, string? expectedVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(document);
        if (expectedVersion == null) return Task.FromResult(_documents.TryAdd(document.Id, json));
        if (!_documents.TryGetValue(document.Id, out var previous) ||
            JsonSerializer.Deserialize<OAuthDocument>(previous)!.Version != expectedVersion) return Task.FromResult(false);
        return Task.FromResult(_documents.TryUpdate(document.Id, json, previous));
    }

    public Task<long> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        long removed = 0;
        foreach (var pair in _documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = JsonSerializer.Deserialize<OAuthDocument>(pair.Value)!;
            if (document.DeleteAfterUtc is { } expiry && expiry <= now &&
                ((ICollection<KeyValuePair<string, string>>)_documents).Remove(pair)) removed++;
        }
        return Task.FromResult(removed);
    }
}
