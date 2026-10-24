using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RepositoryFramework;

namespace Fantasoccer.Infrastructure;

// Rystem's standard blob backend does not offer conditional updates. Keep its exact
// entity envelope, container and key layout, adding CAS only at this boundary.
public sealed class BlobOAuthDocumentStorage(IRepository<OAuthDocument, string> repository,
    BlobContainerClient container) : IOAuthDocumentStorage
{
    public async Task<OAuthDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try { return await repository.GetAsync(id, cancellationToken); }
        catch (RequestFailedException exception) when (exception.Status == 404) { return null; }
    }

    public async IAsyncEnumerable<OAuthDocument> ListAsync(string kind,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Prefix listing avoids scanning unrelated entity kinds. Reads retain Rystem's
        // repository/envelope; a concurrent retention deletion is treated as absent.
        await foreach (var blob in container.GetBlobsAsync(new GetBlobsOptions { Prefix = kind + "-" }, cancellationToken))
        {
            var item = await GetAsync(blob.Name, cancellationToken);
            if (item?.Kind == kind && item.Payload != null) yield return item;
        }
    }

    public async Task<bool> WriteAsync(OAuthDocument document, string? expectedVersion, CancellationToken cancellationToken = default)
    {
        var blob = container.GetBlobClient(document.Id);
        var conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
        try
        {
            if (expectedVersion != null)
            {
                var current = await blob.DownloadContentAsync(cancellationToken);
                var value = JsonSerializer.Deserialize<Entity<OAuthDocument, string>>(current.Value.Content)?.Value;
                if (value?.Version != expectedVersion) return false;
                conditions = new BlobRequestConditions { IfMatch = current.Value.Details.ETag };
            }
            await blob.UploadAsync(BinaryData.FromString(Entity.Default(document, document.Id).ToJson()),
                new BlobUploadOptions { Conditions = conditions }, cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412) { return false; }
    }

    public async Task<long> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        long removed = 0;
        await foreach (var item in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var blob = container.GetBlobClient(item.Name);
            try
            {
                var current = await blob.DownloadContentAsync(cancellationToken);
                var document = JsonSerializer.Deserialize<Entity<OAuthDocument, string>>(current.Value.Content)?.Value;
                if (document?.DeleteAfterUtc is not { } expiry || expiry > now) continue;
                await blob.DeleteAsync(conditions: new BlobRequestConditions { IfMatch = current.Value.Details.ETag }, cancellationToken: cancellationToken);
                removed++;
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 412) { }
        }
        return removed;
    }
}
