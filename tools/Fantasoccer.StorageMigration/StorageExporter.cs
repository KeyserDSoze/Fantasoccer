using Azure;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace Fantasoccer.StorageMigration;

public sealed class StorageExporter(StorageEndpoints source, int pageSize = 500)
{
    public async Task<DumpManifest> ExportAsync(string directory, bool resume = false,
        bool sourceQuiesced = false, bool allowRemoteSource = false, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        source.RequireExportPermission(allowRemoteSource);
        var files = new DumpFiles(directory);
        if (!resume && files.HasEntries)
            throw new InvalidOperationException("Use an empty destination or explicitly --resume an existing dump.");
        using var lease = files.Lock();
        if (!resume && files.Exists)
            throw new InvalidOperationException("A dump already exists. Use --resume explicitly.");
        var previous = resume
            ? await files.ReadManifestAsync(cancellationToken)
            : new DumpManifest { SourceId = source.SourceId, SourceAccountName = source.Blobs.AccountName };
        if (previous.SourceId != source.SourceId || previous.SourceAccountName != source.Blobs.AccountName)
            throw new InvalidOperationException("Cannot resume a dump from a different source.");

        var manifest = new DumpManifest
        {
            SourceId = source.SourceId, ExportId = previous.ExportId,
            SourceAccountName = source.Blobs.AccountName,
            SourceQuiesced = sourceQuiesced
        };
        // Keep the previous completed checkpoints until a new one is atomically saved.
        var reusable = previous.Containers.SelectMany(container => container.Blobs.Select(blob =>
            (Key: (container.Name, blob.Name), Blob: blob))).ToDictionary(x => x.Key, x => x.Blob);
        // Persist a resumable starting point even when cancellation was just requested.
        await files.WriteManifestAsync(manifest, CancellationToken.None);

        try
        {
            await foreach (var containerPage in source.Blobs.GetBlobContainersAsync(BlobContainerTraits.Metadata,
                               cancellationToken: cancellationToken).AsPages(pageSizeHint: pageSize))
            foreach (var container in containerPage.Values)
            {
                var entry = new ContainerDump(container.Name, container.Properties.Metadata is { } metadata ? new(metadata) : []);
                manifest.Containers.Add(entry);
                var client = source.Blobs.GetBlobContainerClient(container.Name);
                await foreach (var blobPage in client.GetBlobsAsync(cancellationToken: cancellationToken).AsPages(pageSizeHint: pageSize))
                foreach (var blob in blobPage.Values)
                {
                    var path = files.BlobPath(container.Name, blob.Name);
                    if (reusable.TryGetValue((container.Name, blob.Name), out var cached) &&
                        // Listing XML and download headers may differ only by ETag quotes.
                        string.Equals(cached.ETag.Trim('"'), blob.Properties.ETag?.ToString().Trim('"'), StringComparison.Ordinal) &&
                        await DumpFiles.MatchesAsync(path, cached.Length, cached.Sha256, cancellationToken))
                    {
                        entry.Blobs.Add(cached);
                    }
                    else
                    {
                        if (blob.Properties.ETag is not { } etag)
                            throw new InvalidDataException("Blob listing did not provide a version for conditional download.");
                        var response = await client.GetBlobClient(blob.Name).DownloadStreamingAsync(new BlobDownloadOptions
                        {
                            Conditions = new BlobRequestConditions { IfMatch = etag }
                        }, cancellationToken);
                        await using var content = response.Value.Content;
                        await DumpFiles.WriteAtomicAsync(path, stream => content.CopyToAsync(stream, cancellationToken), cancellationToken);
                        var fingerprint = await DumpFiles.FingerprintAsync(path, cancellationToken);
                        var details = response.Value.Details;
                        entry.Blobs.Add(new(blob.Name, details.ETag.ToString(), fingerprint.Length, fingerprint.Sha256,
                            new(details.Metadata), new(details.ContentType, details.ContentEncoding, details.ContentLanguage,
                                details.ContentDisposition, details.CacheControl, details.ContentHash)));
                    }

                    // A crash may cause up to 49 blobs to be downloaded again, never skipped unchecked.
                    if (entry.Blobs.Count % 50 == 0) await files.WriteManifestAsync(manifest, cancellationToken);
                }
                await files.WriteManifestAsync(manifest, cancellationToken);
            }

            await foreach (var table in source.Tables.QueryAsync(maxPerPage: pageSize, cancellationToken: cancellationToken))
            {
                var path = files.TablePath(table.Name);
                long count = 0;
                // Table snapshots are rewritten on resume: no unsafe continuation across source changes.
                await DumpFiles.WriteAtomicAsync(path, async stream =>
                {
                    await foreach (var entity in source.Tables.GetTableClient(table.Name)
                                       .QueryAsync<Azure.Data.Tables.TableEntity>(maxPerPage: pageSize, cancellationToken: cancellationToken))
                    {
                        await JsonSerializer.SerializeAsync(stream, TableValueCodec.Encode(entity), DumpJson.Compact, cancellationToken);
                        await stream.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
                        count++;
                    }
                }, cancellationToken);
                var fingerprint = await DumpFiles.FingerprintAsync(path, cancellationToken);
                manifest.Tables.Add(new(table.Name, count, fingerprint.Length, fingerprint.Sha256));
                await files.WriteManifestAsync(manifest, cancellationToken);
            }

            manifest.Complete = true;
            manifest.CompletedAtUtc = DateTimeOffset.UtcNow;
            await files.WriteManifestAsync(manifest, cancellationToken);
            return manifest;
        }
        catch (Exception exception)
        {
            manifest.Complete = false;
            manifest.CompletedAtUtc = null;
            manifest.FailureCode = exception switch
            {
                RequestFailedException azure => azure.ErrorCode ?? "StorageRequestFailed",
                OperationCanceledException => "Cancelled",
                _ => "ExportFailed"
            };
            await files.WriteManifestAsync(manifest, CancellationToken.None);
            throw;
        }
    }
}
