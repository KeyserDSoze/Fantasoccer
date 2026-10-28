using Microsoft.Extensions.Caching.Distributed;

namespace Fantasoccer.Caching;

// IDistributedCache is the standard read/write surface. Coordinated publishers
// additionally need optimistic concurrency and an explicit, expiring lease:
// IDistributedCache alone is neither a compare-and-swap store nor a lock.
public sealed record DistributedCacheEntry(byte[] Value, string ETag, DateTimeOffset? ExpiresAtUtc);

public sealed record DistributedCachePurgeResult(int Scanned, int Deleted);

public interface IDistributedCacheLease : IAsyncDisposable
{
    string Key { get; }
    // Canceled when ownership can no longer be guaranteed (renewal failed).
    CancellationToken LeaseLost { get; }
}

public interface IDistributedCacheCoordinator
{
    // Same logical expiration rules as IDistributedCache.Get, plus the version tag.
    Task<DistributedCacheEntry?> GetEntryAsync(string key, CancellationToken cancellationToken = default);

    // Conditional write. expectedETag == null means "only if absent or logically
    // expired". Returns the new ETag, or null when another writer won the race.
    Task<string?> TrySetAsync(string key, byte[] value, DistributedCacheEntryOptions options, string? expectedETag,
        CancellationToken cancellationToken = default);

    // Non-blocking: returns null when another instance holds the lease. Leases
    // always expire on their own, so a crashed holder never leaves a permanent lock.
    Task<IDistributedCacheLease?> TryAcquireLeaseAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default);

    // Deletes entries whose logical expiration passed more than `grace` ago.
    Task<DistributedCachePurgeResult> PurgeExpiredAsync(TimeSpan grace, int maximumItems, CancellationToken cancellationToken = default);
}
