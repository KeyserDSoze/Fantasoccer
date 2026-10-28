using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Fantasoccer.Infrastructure.Caching;

public sealed class AzureBlobDistributedCacheOptions
{
    public const string SectionName = "Cache";
    // Falls back to ConnectionStrings:Storage in the API registration.
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "fantazone-cache";
    // First path segment. Separates environments that share a storage account.
    public string Namespace { get; set; } = "default";
    public bool CreateContainerIfNotExists { get; set; } = true;
    // Sliding entries rewrite their access metadata at most once per this
    // fraction of the window, instead of a Blob write on every read.
    public double SlidingRefreshRatio { get; set; } = 0.1;
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
    // Abandoned lock blobs (not leased) are removed after this age.
    public TimeSpan LockBlobRetention { get; set; } = TimeSpan.FromDays(1);
}

// Provider-owned expiration metadata, stored as Blob metadata so purge scans do
// not need to download content. The payload itself stays the caller's UTF-8 JSON.
internal readonly record struct CacheExpiration(DateTimeOffset CreatedAtUtc, DateTimeOffset? AbsoluteUtc, TimeSpan? Sliding, DateTimeOffset LastAccessUtc)
{
    public const string FormatVersion = "1";
    private const string VersionKey = "fzcache";
    private const string CreatedKey = "createdat";
    private const string AbsoluteKey = "absoluteexpiration";
    private const string SlidingKey = "slidingexpiration";
    private const string AccessKey = "lastaccess";
    private const string ExpiresKey = "expiresat";

    public DateTimeOffset? ExpiresAtUtc => (AbsoluteUtc, Sliding) switch
    {
        (null, null) => null,
        ({ } absolute, null) => absolute,
        (null, { } sliding) => LastAccessUtc + sliding,
        ({ } absolute, { } sliding) => LastAccessUtc + sliding < absolute ? LastAccessUtc + sliding : absolute
    };

    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc is { } expires && now >= expires;

    public bool NeedsTouch(DateTimeOffset now, double ratio) =>
        Sliding is { } sliding && (now - LastAccessUtc) >= TimeSpan.FromTicks((long)(sliding.Ticks * Math.Clamp(ratio, 0, 1)));

    public CacheExpiration Touch(DateTimeOffset now) => this with { LastAccessUtc = now };

    public static CacheExpiration Create(DateTimeOffset now, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DateTimeOffset? absolute = null;
        if (options.AbsoluteExpirationRelativeToNow is { } relative)
        {
            if (relative <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options), relative, "The relative expiration value must be positive.");
            absolute = now + relative;
        }
        else if (options.AbsoluteExpiration is { } fixedAt)
        {
            if (fixedAt <= now) throw new ArgumentOutOfRangeException(nameof(options), fixedAt, "The absolute expiration value must be in the future.");
            absolute = fixedAt;
        }
        if (options.SlidingExpiration is { } sliding && sliding <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), sliding, "The sliding expiration value must be positive.");
        return new(now, absolute, options.SlidingExpiration, now);
    }

    public Dictionary<string, string> ToMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [VersionKey] = FormatVersion,
            [CreatedKey] = Unix(CreatedAtUtc),
            [AccessKey] = Unix(LastAccessUtc)
        };
        if (AbsoluteUtc is { } absolute) metadata[AbsoluteKey] = Unix(absolute);
        if (Sliding is { } sliding) metadata[SlidingKey] = ((long)sliding.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        if (ExpiresAtUtc is { } expires) metadata[ExpiresKey] = Unix(expires);
        return metadata;
    }

    // Unknown/corrupted metadata is never trusted: the entry is treated as missing.
    public static bool TryRead(IDictionary<string, string>? metadata, out CacheExpiration expiration)
    {
        expiration = default;
        if (metadata is null || !metadata.TryGetValue(VersionKey, out var version) || version != FormatVersion) return false;
        if (!TryUnix(metadata, CreatedKey, out var created) || !TryUnix(metadata, AccessKey, out var access)) return false;
        DateTimeOffset? absolute = null; TimeSpan? sliding = null;
        if (metadata.ContainsKey(AbsoluteKey))
        {
            if (!TryUnix(metadata, AbsoluteKey, out var value)) return false;
            absolute = value;
        }
        if (metadata.TryGetValue(SlidingKey, out var raw))
        {
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0) return false;
            sliding = TimeSpan.FromMilliseconds(milliseconds);
        }
        expiration = new(created, absolute, sliding, access);
        return true;
    }

    private static string Unix(DateTimeOffset value) => value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    private static bool TryUnix(IDictionary<string, string> metadata, string key, out DateTimeOffset value)
    {
        value = default;
        if (!metadata.TryGetValue(key, out var raw) || !long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var milliseconds)) return false;
        try { value = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}

public static class CacheBlobNaming
{
    // Logical keys are ':'-separated lowercase segments, e.g.
    // "football:v1:15:day-08:chances" -> "{namespace}/football/v1/15/day-08/chances.json".
    // Anything else is hashed, so a caller-provided key never escapes its prefix.
    public static string BlobName(string @namespace, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var prefix = Segment(@namespace);
        var segments = key.Split(':');
        if (key.Length <= 512 && segments.All(IsSafeSegment)) return prefix + "/" + string.Join('/', segments) + ".json";
        return prefix + "/_hashed/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json";
    }

    public static string LockName(string @namespace, string key) =>
        Segment(@namespace) + "/_locks/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".lock";

    public static string NamespacePrefix(string @namespace) => Segment(@namespace) + "/";

    public static bool IsLockBlob(string name) => name.EndsWith(".lock", StringComparison.Ordinal) && name.Contains("/_locks/", StringComparison.Ordinal);

    private static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Cache namespace is required.", nameof(value));
        var normalized = value.Trim().ToLowerInvariant();
        if (!IsSafeSegment(normalized)) throw new ArgumentException("Cache namespace must contain only a-z, 0-9, '.', '_' or '-'.", nameof(value));
        return normalized;
    }

    private static bool IsSafeSegment(string segment) => segment.Length is > 0 and <= 128 && segment[0] != '.' && segment[0] != '_' &&
        segment.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.') && !segment.Contains("..", StringComparison.Ordinal);

    internal static bool LooksLikeJson(ReadOnlySpan<byte> value)
    {
        var start = 0;
        while (start < value.Length && value[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') start++;
        if (start == value.Length || value[start] is not ((byte)'{' or (byte)'[')) return false;
        try
        {
            var reader = new Utf8JsonReader(value);
            while (reader.Read()) { }
            return true;
        }
        catch (JsonException) { return false; }
    }
}
