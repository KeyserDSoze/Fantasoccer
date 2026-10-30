using System.Text.Json;
using Fantasoccer.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Caching;

// Stable, versioned logical keys. They never contain emails, tokens or names.
// The Blob provider maps them to ordered paths, e.g. football/v1/15/day-08/chances.json.
public static class CacheKeys
{
    public const int FootballSchemaVersion = 1;
    public const int LiveSchemaVersion = 1;
    public static string FootballChances(int season, int day) => $"football:v{FootballSchemaVersion}:{Season(season)}:{Day(day)}:chances";
    public static string FootballLiveVotes(int season, int day) => $"football:v{FootballSchemaVersion}:{Season(season)}:{Day(day)}:live-votes";
    // Group-scoped projection: the only key containing a group identifier.
    public static string LiveGroup(Guid group, int season, int day) => $"live:v{LiveSchemaVersion}:{group:N}:{Season(season)}:{Day(day)}";
    public static string RegenerationLock(string key) => key + ":regeneration";
    private static string Season(int season) => season is >= 1 and <= 200 ? season.ToString(System.Globalization.CultureInfo.InvariantCulture) : throw new ArgumentOutOfRangeException(nameof(season));
    private static string Day(int day) => day is >= 0 and <= 38 ? "day-" + day.ToString("00", System.Globalization.CultureInfo.InvariantCulture) : throw new ArgumentOutOfRangeException(nameof(day));
}

// Application envelope stored as the Blob content. SourceRevision orders
// publications (a newer one is never replaced by an older one); the fingerprint
// identifies the exact source data a projection was computed from. For snapshots
// that mirror an external source it is a hash of the payload, so "same content"
// is recognised without comparing documents.
// GeneratedAtUtc is the last time the content was produced or re-confirmed;
// FreshUntilUtc is how long that confirmation is considered current.
public sealed record CacheEnvelope<T>(int SchemaVersion, string Kind, DateTimeOffset GeneratedAtUtc, DateTimeOffset FreshUntilUtc,
    long SourceRevision, string? SourceFingerprint, T Payload)
{
    public bool IsFreshAt(DateTimeOffset now) => now <= FreshUntilUtc;
}

public sealed record CacheRead<T>(CacheEnvelope<T> Envelope, bool IsStale);
public enum CachePublishOutcome { Published, Unchanged, SkippedNewerExists, Conflict }

public static class CacheContent
{
    // Stable identity of a payload: the same content always hashes the same.
    public static string Fingerprint<T>(T payload) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, SharedSnapshotCache.Json)));
}

public sealed class SharedCacheOptions
{
    // L1 is an optimization only: short enough to bound cross-instance staleness.
    public TimeSpan MemoryTimeToLive { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan RegenerationLease { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RegenerationWait { get; set; } = TimeSpan.FromSeconds(3);
    public int PublishAttempts { get; set; } = 5;
}

public sealed class SharedSnapshotCache(IDistributedCacheCoordinator store, IMemoryCache memory, TimeProvider clock,
    IOptions<SharedCacheOptions> options, ILogger<SharedSnapshotCache> logger)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CacheEnvelope<T>?> ReadAsync<T>(string key, string kind, int schemaVersion, CancellationToken cancellationToken = default)
    {
        if (memory.TryGetValue(MemoryKey(key), out CacheEnvelope<T>? cached) && cached is not null) return cached;
        return await ReadDistributedAsync<T>(key, kind, schemaVersion, cancellationToken);
    }

    // Every Blob overwrite leaves a soft-deleted copy behind when the account has
    // soft delete enabled, so an identical document is never rewritten: with
    // `confirmEvery` set, the same fingerprint is only republished once the stored
    // one was last confirmed that long ago (the heartbeat that keeps its freshness
    // honest). Without it only the exact same revision and fingerprint is skipped.
    public async Task<CachePublishOutcome> PublishAsync<T>(string key, CacheEnvelope<T> envelope, TimeSpan timeToLive,
        Func<CacheEnvelope<T>, CacheEnvelope<T>, CacheEnvelope<T>?>? merge = null, TimeSpan? confirmEvery = null, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < Math.Max(1, options.Value.PublishAttempts); attempt++)
        {
            var entry = await store.GetEntryAsync(key, cancellationToken);
            var current = Decode<T>(entry, envelope.Kind, envelope.SchemaVersion);
            var next = envelope;
            if (current is not null)
            {
                // Monotonic: an older computation never overwrites a newer one,
                // even if it finishes last.
                if (current.SourceRevision > envelope.SourceRevision) return CachePublishOutcome.SkippedNewerExists;
                if (merge is not null)
                {
                    var merged = merge(current, envelope);
                    if (merged is null) return CachePublishOutcome.Unchanged;
                    next = merged;
                }
                if (current.SourceFingerprint == next.SourceFingerprint &&
                    (merge is null && current.SourceRevision == next.SourceRevision ||
                     next.SourceFingerprint is not null && confirmEvery is { } interval && next.GeneratedAtUtc - current.GeneratedAtUtc < interval))
                    return CachePublishOutcome.Unchanged;
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(next, Json);
            var written = await store.TrySetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
                entry?.ETag, cancellationToken);
            if (written is not null)
            {
                Remember(key, next);
                return CachePublishOutcome.Published;
            }
            await Task.Delay(Backoff(attempt), cancellationToken);
        }
        logger.LogWarning("Cache publication for {CacheKind} lost every optimistic-concurrency attempt; the stored entry was left unchanged.", envelope.Kind);
        return CachePublishOutcome.Conflict;
    }

    // Stale-while-revalidate with explicit coordination: one instance regenerates
    // under a Blob lease; others serve the previous projection marked stale (only
    // within maxStale) or wait a bounded time. Readers are never blocked forever.
    public async Task<CacheRead<T>> GetOrRegenerateAsync<T>(string key, string kind, int schemaVersion, Func<CacheEnvelope<T>, bool> isCurrent,
        Func<CancellationToken, Task<CacheEnvelope<T>>> regenerate, TimeSpan timeToLive, TimeSpan maxStale, CancellationToken cancellationToken = default)
    {
        var cached = await ReadAsync<T>(key, kind, schemaVersion, cancellationToken);
        if (cached is not null && isCurrent(cached)) return new(cached, false);
        var shared = await ReadDistributedAsync<T>(key, kind, schemaVersion, cancellationToken);
        if (shared is not null && isCurrent(shared)) return new(shared, false);
        cached = Newest(cached, shared);
        IDistributedCacheLease? lease = null;
        try { lease = await store.TryAcquireLeaseAsync(CacheKeys.RegenerationLock(key), options.Value.RegenerationLease, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { logger.LogWarning("Regeneration lease unavailable for {CacheKind}; computing without publishing.", kind); }
        if (lease is not null)
        {
            await using (lease)
            {
                var latest = await ReadDistributedAsync<T>(key, kind, schemaVersion, cancellationToken);
                if (latest is not null && isCurrent(latest)) return new(latest, false);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.LeaseLost);
                var built = await regenerate(linked.Token);
                if (!lease.LeaseLost.IsCancellationRequested)
                {
                    try { await PublishAsync(key, built, timeToLive, cancellationToken: cancellationToken); }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    { logger.LogWarning("Regenerated {CacheKind} could not be published; it is returned uncached.", kind); }
                }
                return new(built, false);
            }
        }
        if (cached is not null && clock.GetUtcNow() - cached.GeneratedAtUtc <= maxStale) return new(cached, true);
        // Bounded wall-clock wait (independent of the logical clock used for freshness).
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (waited.Elapsed < options.Value.RegenerationWait)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            var latest = await ReadDistributedAsync<T>(key, kind, schemaVersion, cancellationToken);
            if (latest is not null && isCurrent(latest)) return new(latest, false);
        }
        // The other instance is slow or gone: compute locally, do not publish.
        return new(await regenerate(cancellationToken), false);
    }

    public void Forget(string key) => memory.Remove(MemoryKey(key));

    private async Task<CacheEnvelope<T>?> ReadDistributedAsync<T>(string key, string kind, int schemaVersion, CancellationToken cancellationToken)
    {
        DistributedCacheEntry? entry;
        try { entry = await store.GetEntryAsync(key, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Cache loss/outage is never fatal: callers rebuild from PostgreSQL or report no data.
            logger.LogWarning("Distributed cache read failed for {CacheKind}.", kind);
            return null;
        }
        var envelope = Decode<T>(entry, kind, schemaVersion);
        if (envelope is not null) Remember(key, envelope);
        return envelope;
    }

    private CacheEnvelope<T>? Decode<T>(DistributedCacheEntry? entry, string kind, int schemaVersion)
    {
        if (entry is null) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(entry.Value, Json);
            if (envelope is null || envelope.SchemaVersion != schemaVersion || envelope.Kind != kind || envelope.Payload is null) return null;
            return envelope;
        }
        catch (JsonException)
        {
            logger.LogWarning("Discarding an unreadable {CacheKind} cache document.", kind);
            return null;
        }
    }

    private void Remember<T>(string key, CacheEnvelope<T> envelope) =>
        memory.Set(MemoryKey(key), envelope, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Value.MemoryTimeToLive, Size = 1 });

    private static CacheEnvelope<T>? Newest<T>(CacheEnvelope<T>? first, CacheEnvelope<T>? second) =>
        first is null ? second : second is null ? first : second.SourceRevision >= first.SourceRevision ? second : first;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(1000, 25 * Math.Pow(2, attempt)) + Random.Shared.Next(0, 25));

    private static string MemoryKey(string key) => "fz-l1:" + key;
}
