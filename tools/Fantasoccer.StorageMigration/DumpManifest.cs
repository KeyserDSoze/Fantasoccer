using System.Text.Json;

namespace Fantasoccer.StorageMigration;

public sealed class DumpManifest
{
    public int FormatVersion { get; set; } = 1;
    public string Scope { get; set; } = "blob-table-current-state";
    public Guid ExportId { get; set; } = Guid.NewGuid();
    public required string SourceId { get; set; }
    public required string SourceAccountName { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool SourceQuiesced { get; set; }
    public bool Complete { get; set; }
    public List<string> Exclusions { get; set; } =
        ["Queues", "FileShares", "BlobVersions", "BlobSnapshots", "SoftDeletedData", "BlobTagsAndAccessPolicies", "BlobStorageTierAndType"];
    public List<ContainerDump> Containers { get; set; } = [];
    public List<TableDump> Tables { get; set; } = [];
    public string? FailureCode { get; set; }
}

public sealed record ContainerDump(string Name, Dictionary<string, string> Metadata)
{
    public List<BlobDump> Blobs { get; init; } = [];
}

public sealed record BlobDump(string Name, string ETag, long Length, string Sha256,
    Dictionary<string, string> Metadata, BlobHeaders Headers);

public sealed record BlobHeaders(string? ContentType, string? ContentEncoding, string? ContentLanguage,
    string? ContentDisposition, string? CacheControl, byte[]? ContentHash);

public sealed record TableDump(string Name, long EntityCount, long Length, string Sha256);

public sealed record TableRow(string PartitionKey, string RowKey, DateTimeOffset? Timestamp,
    string ETag, Dictionary<string, TableValue> Properties);

public sealed record TableValue(string Type, JsonElement Value);

internal static class DumpJson
{
    public static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    public static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
