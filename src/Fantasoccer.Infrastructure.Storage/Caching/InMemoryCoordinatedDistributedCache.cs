using System.Collections.Concurrent;
using System.Globalization;
using Fantasoccer.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace Fantasoccer.Infrastructure.Caching;

// Process-local double with the same logical semantics as the Blob provider
// (expiration, ETags, leases). Used by the InMemory/test configuration only.
public sealed class InMemoryCoordinatedDistributedCache(TimeProvider clock) : IDistributedCache, IDistributedCacheCoordinator
{
    private sealed record Item(byte[] Value, long Version, CacheExpiration Expiration);
    private readonly ConcurrentDictionary<string, Item> _items = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (Guid Owner, DateTimeOffset Until)> _leases = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _version;

    public byte[]? Get(string key) => GetEntry(key)?.Value;
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(GetEntry(key)?.Value);
    public Task<DistributedCacheEntry?> GetEntryAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(GetEntry(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => TrySet(key, value, options, null, unconditional: true);
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    { Set(key, value, options); return Task.CompletedTask; }

    public Task<string?> TrySetAsync(string key, byte[] value, DistributedCacheEntryOptions options, string? expectedETag, CancellationToken cancellationToken = default)
        => Task.FromResult(TrySet(key, value, options, expectedETag, unconditional: false));

    public void Refresh(string key)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (_items.TryGetValue(key, out var item) && !item.Expiration.IsExpired(now) && item.Expiration.Sliding is not null)
                _items[key] = item with { Version = Interlocked.Increment(ref _version), Expiration = item.Expiration.Touch(now) };
        }
    }
    public Task RefreshAsync(string key, CancellationToken token = default) { Refresh(key); return Task.CompletedTask; }

    public void Remove(string key) => _items.TryRemove(key, out _);
    public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }

    public Task<IDistributedCacheLease?> TryAcquireLeaseAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var owner = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var until = now + TimeSpan.FromSeconds(Math.Clamp(Math.Ceiling(duration.TotalSeconds), 15, 60));
        lock (_gate)
        {
            if (_leases.TryGetValue(key, out var current) && current.Until > now) return Task.FromResult<IDistributedCacheLease?>(null);
            _leases[key] = (owner, until);
        }
        return Task.FromResult<IDistributedCacheLease?>(new Lease(this, key, owner));
    }

    public Task<DistributedCachePurgeResult> PurgeExpiredAsync(TimeSpan grace, int maximumItems, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow(); var scanned = 0; var deleted = 0;
        foreach (var (key, item) in _items.ToArray())
        {
            if (scanned++ >= maximumItems) break;
            if (item.Expiration.ExpiresAtUtc is { } expires && expires + grace < now &&
                ((ICollection<KeyValuePair<string, Item>>)_items).Remove(new(key, item))) deleted++;
        }
        return Task.FromResult(new DistributedCachePurgeResult(Math.Min(scanned, maximumItems), deleted));
    }

    public int Count => _items.Count;

    private DistributedCacheEntry? GetEntry(string key)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var item) || item.Expiration.IsExpired(now)) return null;
            if (item.Expiration.Sliding is not null)
                _items[key] = item = item with { Version = Interlocked.Increment(ref _version), Expiration = item.Expiration.Touch(now) };
            return new(item.Value.ToArray(), Tag(item.Version), item.Expiration.ExpiresAtUtc);
        }
    }

    private string? TrySet(string key, byte[] value, DistributedCacheEntryOptions options, string? expectedETag, bool unconditional)
    {
        ArgumentNullException.ThrowIfNull(value);
        var now = clock.GetUtcNow();
        var expiration = CacheExpiration.Create(now, options);
        lock (_gate)
        {
            _items.TryGetValue(key, out var current);
            var live = current is not null && !current.Expiration.IsExpired(now);
            if (!unconditional)
            {
                if (expectedETag is null && live) return null;
                if (expectedETag is not null && (current is null || Tag(current.Version) != expectedETag)) return null;
            }
            var version = Interlocked.Increment(ref _version);
            _items[key] = new(value.ToArray(), version, expiration);
            return Tag(version);
        }
    }

    private static string Tag(long version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";

    private sealed class Lease(InMemoryCoordinatedDistributedCache owner, string key, Guid id) : IDistributedCacheLease
    {
        private readonly CancellationTokenSource _lost = new();
        public string Key => key;
        public CancellationToken LeaseLost => _lost.Token;
        public ValueTask DisposeAsync()
        {
            lock (owner._gate)
                if (owner._leases.TryGetValue(key, out var current) && current.Owner == id) owner._leases.TryRemove(key, out _);
            _lost.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
