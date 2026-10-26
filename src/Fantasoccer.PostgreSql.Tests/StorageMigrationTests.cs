using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Fantasoccer.StorageMigration;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE")))
            Skip = "Run scripts/migration/test-local.sh to test storage export/restore with dedicated Azurite accounts.";
    }
}

public sealed class StorageMigrationTests : IAsyncLifetime
{
    private StorageEndpoints _source = null!;
    private StorageEndpoints _target = null!;
    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private string Container => "export-" + _suffix;
    private string EmptyContainer => "empty-" + _suffix;
    private string Table => "Export" + _suffix;
    private string EmptyTable => "Empty" + _suffix;
    private const string BlobName = "documenti/rosa|||owner@example.test à.json";
    private readonly byte[] _payload = "{\"Key\":\"group|||15|||basket|||owner\",\"Value\":{\"n\":\"Squadra\",\"p\":[]}}"u8.ToArray();
    private string _dumpDirectory = null!;

    public async ValueTask InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE");
        if (string.IsNullOrWhiteSpace(connection)) return;
        _source = new(connection);
        _target = new(Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE_RESTORE")
                      ?? throw new InvalidOperationException("Set the dedicated restore test account."));
        _source.RequireLocalEmulator();
        _target.RequireLocalEmulator();
        if (_source.Blobs.AccountName != "migrationtest" || _target.Blobs.AccountName != "migrationrestore")
            throw new InvalidOperationException("Tests only use the dedicated migrationtest/migrationrestore Azurite accounts.");

        var temporaryRoot = Directory.Exists("/tmp/opencode") ? "/tmp/opencode" : Path.GetTempPath();
        _dumpDirectory = Path.Combine(temporaryRoot, "fantasoccer-storage-test-" + _suffix);
        var container = _source.Blobs.GetBlobContainerClient(Container);
        await container.CreateAsync(metadata: new Dictionary<string, string> { ["purpose"] = "migration-test" });
        await _source.Blobs.GetBlobContainerClient(EmptyContainer).CreateAsync();
        await container.GetBlobClient(BlobName).UploadAsync(BinaryData.FromBytes(_payload), new BlobUploadOptions
        {
            Metadata = new Dictionary<string, string> { ["factory"] = "day" },
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json", CacheControl = "no-cache" }
        });
        await _source.Tables.CreateTableAsync(Table);
        await _source.Tables.CreateTableAsync(EmptyTable);
        await _source.Tables.GetTableClient(Table).AddEntityAsync(new TableEntity("partition", "owner@example.test")
        {
            ["Document"] = "{\"g\":\"group\",\"y\":15}",
            ["Int32"] = 42,
            ["Int64"] = long.MaxValue,
            ["Boolean"] = true,
            ["Binary"] = new byte[] { 0, 127, 255 },
            ["Double"] = 1.25d,
            ["Guid"] = Guid.Parse("bf16ec81-9db2-4d88-9eb4-d282097d3acb"),
            ["Date"] = DateTimeOffset.Parse("2026-10-25T12:00:00+00:00")
        });
    }

    [AzuriteFact]
    public async Task Export_preserves_current_payloads_metadata_and_typed_tables_without_source_writes()
    {
        var originalBlob = await _source.Blobs.GetBlobContainerClient(Container).GetBlobClient(BlobName).GetPropertiesAsync();
        var originalEntity = await _source.Tables.GetTableClient(Table).GetEntityAsync<TableEntity>("partition", "owner@example.test");
        var manifest = await new StorageExporter(_source).ExportAsync(_dumpDirectory, sourceQuiesced: true);
        var verified = await DumpArchive.VerifyAsync(_dumpDirectory);

        Assert.True(manifest.Complete);
        Assert.True(verified.SourceQuiesced);
        Assert.Contains(manifest.Exclusions, value => value == "Queues");
        Assert.Empty(manifest.Containers.Single(x => x.Name == EmptyContainer).Blobs);
        Assert.Equal(0, manifest.Tables.Single(x => x.Name == EmptyTable).EntityCount);
        Assert.Equal(1, manifest.Tables.Single(x => x.Name == Table).EntityCount);
        Assert.Equal(_payload, await File.ReadAllBytesAsync(Directory.GetFiles(Path.Combine(_dumpDirectory, "blobs")).Single()));
        Assert.Equal(originalBlob.Value.ETag, (await _source.Blobs.GetBlobContainerClient(Container).GetBlobClient(BlobName).GetPropertiesAsync()).Value.ETag);
        Assert.Equal(originalEntity.Value.ETag, (await _source.Tables.GetTableClient(Table).GetEntityAsync<TableEntity>("partition", "owner@example.test")).Value.ETag);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dumpDirectory, "manifest.json")));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_dumpDirectory));
        }
    }

    [AzuriteFact]
    public async Task Local_restore_can_run_twice_and_preserves_original_document_bytes_and_Table_types()
    {
        await new StorageExporter(_source).ExportAsync(_dumpDirectory);
        await DumpArchive.RestoreLocalAsync(_dumpDirectory, _target);
        await DumpArchive.RestoreLocalAsync(_dumpDirectory, _target);
        var container = _target.Blobs.GetBlobContainerClient(Container);
        var blob = container.GetBlobClient(BlobName);
        Assert.Equal(_payload, (await blob.DownloadContentAsync()).Value.Content.ToArray());
        var properties = (await blob.GetPropertiesAsync()).Value;
        Assert.Equal("application/json", properties.ContentType);
        Assert.Equal("no-cache", properties.CacheControl);
        Assert.Equal("day", properties.Metadata["factory"]);
        Assert.Equal("migration-test", (await container.GetPropertiesAsync()).Value.Metadata["purpose"]);
        Assert.True((await _target.Blobs.GetBlobContainerClient(EmptyContainer).ExistsAsync()).Value);
        Assert.True(await TableExistsAsync(_target, EmptyTable));

        var original = (await _source.Tables.GetTableClient(Table).GetEntityAsync<TableEntity>("partition", "owner@example.test")).Value;
        var restored = (await _target.Tables.GetTableClient(Table).GetEntityAsync<TableEntity>("partition", "owner@example.test")).Value;
        foreach (var (key, value) in original.Where(x => x.Key is not ("Timestamp" or "odata.etag")))
        {
            Assert.Equal(value.GetType(), restored[key].GetType());
            if (value is byte[] bytes) Assert.Equal(bytes, Assert.IsType<byte[]>(restored[key]));
            else Assert.Equal(value, restored[key]);
        }
    }

    [AzuriteFact]
    public async Task Resume_reuses_verified_blobs_and_repairs_corrupted_local_payloads()
    {
        var exporter = new StorageExporter(_source);
        var first = await exporter.ExportAsync(_dumpDirectory);
        var path = Directory.GetFiles(Path.Combine(_dumpDirectory, "blobs")).Single();
        var originalWrite = File.GetLastWriteTimeUtc(path);
        var second = await exporter.ExportAsync(_dumpDirectory, resume: true);
        Assert.Equal(first.ExportId, second.ExportId);
        Assert.Equal(originalWrite, File.GetLastWriteTimeUtc(path));

        await File.WriteAllBytesAsync(path, new byte[_payload.Length]);
        await exporter.ExportAsync(_dumpDirectory, resume: true);
        Assert.Equal(_payload, await File.ReadAllBytesAsync(path));
        await DumpArchive.VerifyAsync(_dumpDirectory);
    }

    [AzuriteFact]
    public async Task Resume_refreshes_changed_and_removed_source_records_without_stale_manifest_entries()
    {
        var exporter = new StorageExporter(_source);
        await exporter.ExportAsync(_dumpDirectory);
        var container = _source.Blobs.GetBlobContainerClient(Container);
        await container.GetBlobClient(BlobName).DeleteAsync();
        await container.GetBlobClient("new.json").UploadAsync(BinaryData.FromString("new payload"));
        await _source.Tables.GetTableClient(Table).DeleteEntityAsync("partition", "owner@example.test");
        var resumed = await exporter.ExportAsync(_dumpDirectory, resume: true);
        Assert.Equal("new.json", Assert.Single(resumed.Containers.Single(x => x.Name == Container).Blobs).Name);
        Assert.Equal(0, resumed.Tables.Single(x => x.Name == Table).EntityCount);
        await DumpArchive.VerifyAsync(_dumpDirectory);
    }

    [AzuriteFact]
    public async Task Corrupted_archive_is_rejected_before_any_restore_write()
    {
        await new StorageExporter(_source).ExportAsync(_dumpDirectory);
        await File.AppendAllTextAsync(Directory.GetFiles(Path.Combine(_dumpDirectory, "tables")).First(x => new FileInfo(x).Length > 0), "corruption");
        await Assert.ThrowsAsync<InvalidDataException>(() => DumpArchive.RestoreLocalAsync(_dumpDirectory, _target));
        Assert.False((await _target.Blobs.GetBlobContainerClient(Container).ExistsAsync()).Value);
        Assert.False(await TableExistsAsync(_target, Table));
    }

    [AzuriteFact]
    public async Task Incomplete_dump_cannot_be_restored_but_can_be_resumed()
    {
        var exporter = new StorageExporter(_source);
        await exporter.ExportAsync(_dumpDirectory);
        var path = Path.Combine(_dumpDirectory, "manifest.json");
        var manifest = JsonSerializer.Deserialize<DumpManifest>(await File.ReadAllTextAsync(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        manifest.Complete = false;
        manifest.CompletedAtUtc = null;
        manifest.FailureCode = "SimulatedInterruption";
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await Assert.ThrowsAsync<InvalidDataException>(() => DumpArchive.VerifyAsync(_dumpDirectory));
        await exporter.ExportAsync(_dumpDirectory, resume: true);
        Assert.True((await DumpArchive.VerifyAsync(_dumpDirectory)).Complete);
    }

    [AzuriteFact]
    public async Task Cancelled_export_is_marked_incomplete_and_can_be_resumed()
    {
        var exporter = new StorageExporter(_source);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exporter.ExportAsync(_dumpDirectory, cancellationToken: cancellation.Token));
        var manifest = JsonSerializer.Deserialize<DumpManifest>(await File.ReadAllTextAsync(Path.Combine(_dumpDirectory, "manifest.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(manifest.Complete);
        Assert.Equal("Cancelled", manifest.FailureCode);
        await exporter.ExportAsync(_dumpDirectory, resume: true);
        Assert.True((await DumpArchive.VerifyAsync(_dumpDirectory)).Complete);
    }

    [AzuriteFact]
    public async Task Export_follows_all_listing_pages_including_empty_blob_payloads()
    {
        var container = _source.Blobs.GetBlobContainerClient(Container);
        for (var index = 0; index < 3; index++)
        {
            await container.GetBlobClient($"extra-{index}.bin").UploadAsync(BinaryData.FromBytes(new byte[index]));
            await _source.Tables.GetTableClient(Table).AddEntityAsync(new TableEntity("partition", $"row-{index}") { ["Number"] = index });
        }
        var manifest = await new StorageExporter(_source, pageSize: 1).ExportAsync(_dumpDirectory);
        Assert.Equal(2, manifest.Containers.Count);
        Assert.Equal(2, manifest.Tables.Count);
        Assert.Equal(4, manifest.Containers.Single(x => x.Name == Container).Blobs.Count);
        Assert.Equal(4, manifest.Tables.Single(x => x.Name == Table).EntityCount);
        Assert.Contains(manifest.Containers.Single(x => x.Name == Container).Blobs, blob => blob.Length == 0);
        await DumpArchive.VerifyAsync(_dumpDirectory);
    }

    [AzuriteFact]
    public async Task Public_local_files_option_exposes_only_the_known_file_containers()
    {
        var sourceImages = _source.Blobs.GetBlobContainerClient("images");
        var targetImages = _target.Blobs.GetBlobContainerClient("images");
        Assert.False((await sourceImages.ExistsAsync()).Value);
        Assert.False((await targetImages.ExistsAsync()).Value);
        await sourceImages.CreateAsync();
        try
        {
            await sourceImages.GetBlobClient("example.jpg").UploadAsync(BinaryData.FromBytes(_payload));
            await new StorageExporter(_source).ExportAsync(_dumpDirectory);
            await DumpArchive.RestoreLocalAsync(_dumpDirectory, _target, publicLocalFiles: true);
            Assert.Equal(PublicAccessType.Blob, (await targetImages.GetPropertiesAsync()).Value.PublicAccess);
            Assert.Equal(PublicAccessType.None,
                (await _target.Blobs.GetBlobContainerClient(Container).GetPropertiesAsync()).Value.PublicAccess);
            var anonymous = new Azure.Storage.Blobs.BlobClient(targetImages.GetBlobClient("example.jpg").Uri);
            Assert.Equal(_payload, (await anonymous.DownloadContentAsync()).Value.Content.ToArray());
        }
        finally
        {
            await sourceImages.DeleteIfExistsAsync();
            await targetImages.DeleteIfExistsAsync();
        }
    }

    [AzuriteFact]
    public async Task Existing_dump_and_different_resume_source_are_refused()
    {
        await new StorageExporter(_source).ExportAsync(_dumpDirectory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StorageExporter(_source).ExportAsync(_dumpDirectory));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StorageExporter(_target).ExportAsync(_dumpDirectory, resume: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DumpArchive.RestoreLocalAsync(_dumpDirectory, _source));
        var alias = new StorageEndpoints(Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE")!
            .Replace("127.0.0.1", "localhost").Replace("http://azurite:", "http://localhost:"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DumpArchive.RestoreLocalAsync(_dumpDirectory, alias));
    }

    public async ValueTask DisposeAsync()
    {
        if (_source is null) return;
        foreach (var endpoint in new[] { _source, _target })
        {
            await endpoint.Blobs.GetBlobContainerClient(Container).DeleteIfExistsAsync();
            await endpoint.Blobs.GetBlobContainerClient(EmptyContainer).DeleteIfExistsAsync();
            foreach (var name in new[] { Table, EmptyTable })
            {
                try { await endpoint.Tables.DeleteTableAsync(name); }
                catch (Azure.RequestFailedException exception) when (exception.Status == 404) { }
            }
        }
        if (Directory.Exists(_dumpDirectory)) Directory.Delete(_dumpDirectory, recursive: true);
    }

    private static async Task<bool> TableExistsAsync(StorageEndpoints endpoint, string name)
    {
        await foreach (var table in endpoint.Tables.QueryAsync(filter: $"TableName eq '{name}'")) return true;
        return false;
    }
}
