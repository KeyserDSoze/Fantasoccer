using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Azure.Data.Tables;

namespace Fantasoccer.StorageMigration;

public static class DumpArchive
{
    public static async Task<VerifiedJsonDataset> ReadJsonDatasetAsync(string directory, IReadOnlySet<string> containers,
        CancellationToken cancellationToken = default, IReadOnlySet<string>? tables = null)
    {
        var files = new DumpFiles(directory);
        if (!files.Exists) throw new InvalidDataException("Manifest not found.");
        using var lease = files.ReadLock();
        var manifest = await VerifyAsync(files, cancellationToken);
        var documents = new List<LegacyJsonDocument>();
        foreach (var container in manifest.Containers.Where(x => containers.Contains(x.Name)))
        foreach (var blob in container.Blobs)
        {
            if (blob.Length > 64 * 1024 * 1024 || !string.IsNullOrEmpty(blob.Headers.ContentEncoding))
                throw new InvalidDataException("Unsupported legacy document size or encoding.");
            await using var stream = File.OpenRead(files.BlobPath(container.Name, blob.Name));
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            documents.Add(new(container.Name, blob.Name, blob.Sha256, document.RootElement.Clone()));
        }
        foreach (var table in manifest.Tables.Where(x => tables?.Contains(x.Name) == true))
            await foreach (var row in ReadRowsAsync(files.TablePath(table.Name), cancellationToken))
            {
                var encoded = JsonSerializer.SerializeToElement(row, DumpJson.Compact);
                var key = JsonSerializer.Serialize(new[] { row.PartitionKey, row.RowKey });
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(encoded.GetRawText())));
                documents.Add(new("table:" + table.Name, key, hash, encoded));
            }
        return new(manifest, documents);
    }

    public static async Task<DumpManifest> VerifyAsync(string directory, CancellationToken cancellationToken = default)
    {
        var files = new DumpFiles(directory);
        if (!files.Exists) throw new InvalidDataException("Manifest not found.");
        using var lease = files.ReadLock();
        return await VerifyAsync(files, cancellationToken);
    }

    private static async Task<DumpManifest> VerifyAsync(DumpFiles files, CancellationToken cancellationToken)
    {
        var manifest = await files.ReadManifestAsync(cancellationToken);
        if (!manifest.Complete || manifest.CompletedAtUtc is null || manifest.FailureCode is not null)
            throw new InvalidDataException("The export is incomplete. Resume it before verification or restore.");

        foreach (var container in manifest.Containers)
        foreach (var blob in container.Blobs)
            if (!await DumpFiles.MatchesAsync(files.BlobPath(container.Name, blob.Name), blob.Length, blob.Sha256, cancellationToken))
                throw new InvalidDataException("A blob payload is missing or its checksum does not match.");

        foreach (var table in manifest.Tables)
        {
            var path = files.TablePath(table.Name);
            if (!await DumpFiles.MatchesAsync(path, table.Length, table.Sha256, cancellationToken))
                throw new InvalidDataException("A table payload is missing or its checksum does not match.");
            long count = 0;
            var keys = new HashSet<(string PartitionKey, string RowKey)>();
            await foreach (var row in ReadRowsAsync(path, cancellationToken))
            {
                _ = TableValueCodec.Decode(row);
                if (!keys.Add((row.PartitionKey, row.RowKey)))
                    throw new InvalidDataException("Duplicate Table entity in dump.");
                count++;
            }
            if (count != table.EntityCount) throw new InvalidDataException("Table entity count mismatch.");
        }
        return manifest;
    }

    public static async Task<DumpManifest> RestoreLocalAsync(string directory, StorageEndpoints target,
        CancellationToken cancellationToken = default, bool publicLocalFiles = false)
    {
        // This path cannot be enabled for remote Azure, even when remote export is allowed.
        target.RequireLocalEmulator();
        var files = new DumpFiles(directory);
        if (!files.Exists) throw new InvalidDataException("Manifest not found.");
        using var lease = files.ReadLock();
        // Verify the whole archive before the first target write.
        var manifest = await VerifyAsync(files, cancellationToken);
        if (manifest.SourceId == target.SourceId || manifest.SourceAccountName == target.Blobs.AccountName)
            throw new InvalidOperationException("Restore requires a different local target account from the export source.");
        foreach (var container in manifest.Containers)
        {
            var client = target.Blobs.GetBlobContainerClient(container.Name);
            await client.CreateIfNotExistsAsync(PublicAccessType.None, container.Metadata, cancellationToken: cancellationToken);
            await client.SetMetadataAsync(container.Metadata, cancellationToken: cancellationToken);
            if (publicLocalFiles && container.Name is "images" or "logos" or "cardimages")
                await client.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
            foreach (var blob in container.Blobs)
            {
                await using var content = File.OpenRead(files.BlobPath(container.Name, blob.Name));
                await client.GetBlobClient(blob.Name).UploadAsync(content, new BlobUploadOptions
                {
                    Metadata = blob.Metadata,
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = blob.Headers.ContentType, ContentEncoding = blob.Headers.ContentEncoding,
                        ContentLanguage = blob.Headers.ContentLanguage, ContentDisposition = blob.Headers.ContentDisposition,
                        CacheControl = blob.Headers.CacheControl, ContentHash = blob.Headers.ContentHash
                    }
                }, cancellationToken);
            }
        }
        foreach (var table in manifest.Tables)
        {
            var client = target.Tables.GetTableClient(table.Name);
            await client.CreateIfNotExistsAsync(cancellationToken);
            await foreach (var row in ReadRowsAsync(files.TablePath(table.Name), cancellationToken))
                await client.UpsertEntityAsync(TableValueCodec.Decode(row), TableUpdateMode.Replace, cancellationToken);
        }
        return manifest;
    }

    private static async IAsyncEnumerable<TableRow> ReadRowsAsync(string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = File.OpenText(path);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Empty Table record in dump.");
            yield return JsonSerializer.Deserialize<TableRow>(line, DumpJson.Compact)
                ?? throw new InvalidDataException("Invalid Table record in dump.");
        }
    }
}

public sealed record LegacyJsonDocument(string Container, string BlobName, string Sha256, JsonElement Envelope);
public sealed record VerifiedJsonDataset(DumpManifest Manifest, IReadOnlyList<LegacyJsonDocument> Documents);
