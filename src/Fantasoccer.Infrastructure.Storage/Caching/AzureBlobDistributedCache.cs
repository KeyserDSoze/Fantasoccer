using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Fantasoccer.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Infrastructure.Caching;

// IDistributedCache backed by a private Blob container. Each entry is one blob
// holding the caller's UTF-8 JSON bytes; expiration lives in blob metadata.
// Expired blobs may still exist physically: every read checks the logical expiry.
public sealed class AzureBlobDistributedCache : IDistributedCache, IDistributedCacheCoordinator
{
    private readonly BlobContainerClient _container;
    private readonly AzureBlobDistributedCacheOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<AzureBlobDistributedCache> _logger;
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private volatile bool _initialized;

    public AzureBlobDistributedCache(IOptions<AzureBlobDistributedCacheOptions> options, TimeProvider clock, ILogger<AzureBlobDistributedCache>? logger = null)
        : this(CreateContainer(options.Value), options.Value, clock, logger) { }

    public AzureBlobDistributedCache(BlobContainerClient container, AzureBlobDistributedCacheOptions options, TimeProvider clock, ILogger<AzureBlobDistributedCache>? logger = null)
    {
        _container = container;
        _options = options;
        _clock = clock;
        _logger = logger ?? NullLogger<AzureBlobDistributedCache>.Instance;
        CacheBlobNaming.NamespacePrefix(options.Namespace);
    }

    private static BlobContainerClient CreateContainer(AzureBlobDistributedCacheOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString)) throw new InvalidOperationException("Cache:ConnectionString (or ConnectionStrings:Storage) is required for the Blob distributed cache.");
        var client = new BlobClientOptions();
        // Transient faults: SDK-level exponential backoff. Conditional conflicts are
        // returned to the caller, which owns its own bounded retry loop.
        client.Retry.Mode = RetryMode.Exponential;
        client.Retry.MaxRetries = 3;
        client.Retry.Delay = TimeSpan.FromMilliseconds(200);
        client.Retry.MaxDelay = TimeSpan.FromSeconds(4);
        client.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
        return new BlobContainerClient(options.ConnectionString, options.ContainerName, client);
    }

    public byte[]? Get(string key) => GetCoreAsync(key, false, CancellationToken.None).AsTask().GetAwaiter().GetResult()?.Value;
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default) => (await GetCoreAsync(key, true, token))?.Value;
    public Task<DistributedCacheEntry?> GetEntryAsync(string key, CancellationToken cancellationToken = default) => GetCoreAsync(key, true, cancellationToken).AsTask();

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        SetCoreAsync(key, value, options, null, false, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        SetCoreAsync(key, value, options, null, true, token).AsTask();

    public void Refresh(string key) => RefreshCoreAsync(key, false, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    public Task RefreshAsync(string key, CancellationToken token = default) => RefreshCoreAsync(key, true, token).AsTask();

    public void Remove(string key) => RemoveCoreAsync(key, false, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    public Task RemoveAsync(string key, CancellationToken token = default) => RemoveCoreAsync(key, true, token).AsTask();

    public async Task<string?> TrySetAsync(string key, byte[] value, DistributedCacheEntryOptions options, string? expectedETag, CancellationToken cancellationToken = default)
    {
        if (expectedETag is not null)
            return await SetCoreAsync(key, value, options, new BlobRequestConditions { IfMatch = new ETag(expectedETag) }, true, cancellationToken);
        var created = await SetCoreAsync(key, value, options, new BlobRequestConditions { IfNoneMatch = ETag.All }, true, cancellationToken);
        if (created is not null) return created;
        // An existing but logically expired entry counts as absent. Replace it only
        // if it is still that same expired version.
        await EnsureContainerAsync(true, cancellationToken);
        try
        {
            var properties = (await Blob(key).GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
            if (CacheExpiration.TryRead(properties.Metadata, out var state) && !state.IsExpired(_clock.GetUtcNow())) return null;
            return await SetCoreAsync(key, value, options, new BlobRequestConditions { IfMatch = properties.ETag }, true, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return await SetCoreAsync(key, value, options, new BlobRequestConditions { IfNoneMatch = ETag.All }, true, cancellationToken);
        }
    }

    public async Task<IDistributedCacheLease?> TryAcquireLeaseAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(true, cancellationToken);
        var blob = _container.GetBlobClient(CacheBlobNaming.LockName(_options.Namespace, key));
        try
        {
            await blob.UploadAsync(BinaryData.FromString(string.Empty), new BlobUploadOptions
            { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { }
        var lease = blob.GetBlobLeaseClient();
        // Blob leases are 15-60 seconds; they are renewed while held and expire
        // by themselves if the holder disappears.
        var seconds = Math.Clamp(Math.Ceiling(duration.TotalSeconds), 15, 60);
        try { await lease.AcquireAsync(TimeSpan.FromSeconds(seconds), cancellationToken: cancellationToken); }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return null; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
        var renewal = _options.LeaseRenewalInterval <= TimeSpan.Zero || _options.LeaseRenewalInterval >= TimeSpan.FromSeconds(seconds)
            ? TimeSpan.FromSeconds(seconds / 3) : _options.LeaseRenewalInterval;
        return new BlobLease(key, lease, renewal, _logger);
    }

    public async Task<DistributedCachePurgeResult> PurgeExpiredAsync(TimeSpan grace, int maximumItems, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(true, cancellationToken);
        var now = _clock.GetUtcNow();
        var scanned = 0; var deleted = 0;
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, CacheBlobNaming.NamespacePrefix(_options.Namespace), cancellationToken))
        {
            if (scanned++ >= maximumItems) break;
            var modified = item.Properties.LastModified ?? now;
            bool remove;
            if (CacheBlobNaming.IsLockBlob(item.Name))
                remove = item.Properties.LeaseState != LeaseState.Leased && modified + _options.LockBlobRetention < now;
            else if (CacheExpiration.TryRead(item.Metadata, out var state))
                remove = state.ExpiresAtUtc is { } expires && expires + grace < now;
            else
                remove = modified + grace < now; // corrupted or foreign blob inside our namespace
            if (!remove) continue;
            try
            {
                // Only the version observed here: a concurrent rewrite survives.
                await _container.GetBlobClient(item.Name).DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots,
                    new BlobRequestConditions { IfMatch = item.Properties.ETag }, cancellationToken);
                deleted++;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412) { }
        }
        return new(Math.Min(scanned, maximumItems), deleted);
    }

    private async ValueTask<DistributedCacheEntry?> GetCoreAsync(string key, bool async, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(async, cancellationToken);
        var blob = Blob(key);
        BlobDownloadResult result;
        try { result = async ? (await blob.DownloadContentAsync(cancellationToken)).Value : blob.DownloadContent(cancellationToken).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
        var now = _clock.GetUtcNow();
        if (!CacheExpiration.TryRead(result.Details.Metadata, out var state))
        {
            _logger.LogWarning("Distributed cache entry has unreadable expiration metadata and is treated as missing.");
            return null;
        }
        if (state.IsExpired(now)) return null;
        var etag = result.Details.ETag;
        if (state.NeedsTouch(now, _options.SlidingRefreshRatio))
        {
            var touched = state.Touch(now);
            try
            {
                var conditions = new BlobRequestConditions { IfMatch = etag };
                var info = async ? (await blob.SetMetadataAsync(touched.ToMetadata(), conditions, cancellationToken)).Value
                    : blob.SetMetadata(touched.ToMetadata(), conditions, cancellationToken).Value;
                etag = info.ETag; state = touched;
            }
            // Another reader/writer changed it first: their write already moved it on.
            catch (RequestFailedException ex) when (ex.Status is 404 or 412) { }
        }
        return new(result.Content.ToArray(), etag.ToString(), state.ExpiresAtUtc);
    }

    private async ValueTask<string?> SetCoreAsync(string key, byte[] value, DistributedCacheEntryOptions options, BlobRequestConditions? conditions,
        bool async, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var state = CacheExpiration.Create(_clock.GetUtcNow(), options);
        await EnsureContainerAsync(async, cancellationToken);
        var upload = new BlobUploadOptions
        {
            Metadata = state.ToMetadata(), Conditions = conditions,
            HttpHeaders = new BlobHttpHeaders { ContentType = CacheBlobNaming.LooksLikeJson(value) ? "application/json; charset=utf-8" : "application/octet-stream" }
        };
        try
        {
            var data = BinaryData.FromBytes(value);
            var response = async ? await Blob(key).UploadAsync(data, upload, cancellationToken) : Blob(key).Upload(data, upload, cancellationToken);
            return response.Value.ETag.ToString();
        }
        catch (RequestFailedException ex) when (conditions is not null && ex.Status is 409 or 412) { return null; }
    }

    private async ValueTask RefreshCoreAsync(string key, bool async, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(async, cancellationToken);
        var blob = Blob(key);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            BlobProperties properties;
            try { properties = async ? (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value : blob.GetProperties(cancellationToken: cancellationToken).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404) { return; }
            var now = _clock.GetUtcNow();
            if (!CacheExpiration.TryRead(properties.Metadata, out var state) || state.IsExpired(now) || state.Sliding is null) return;
            try
            {
                var conditions = new BlobRequestConditions { IfMatch = properties.ETag };
                if (async) await blob.SetMetadataAsync(state.Touch(now).ToMetadata(), conditions, cancellationToken);
                else blob.SetMetadata(state.Touch(now).ToMetadata(), conditions, cancellationToken);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 412) { }
            catch (RequestFailedException ex) when (ex.Status == 404) { return; }
        }
    }

    private async ValueTask RemoveCoreAsync(string key, bool async, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(async, cancellationToken);
        if (async) await Blob(key).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        else Blob(key).DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    private BlobClient Blob(string key) => _container.GetBlobClient(CacheBlobNaming.BlobName(_options.Namespace, key));

    private async ValueTask EnsureContainerAsync(bool async, CancellationToken cancellationToken)
    {
        if (_initialized) return;
        if (async) await _initialization.WaitAsync(cancellationToken); else _initialization.Wait(cancellationToken);
        try
        {
            if (_initialized) return;
            if (_options.CreateContainerIfNotExists)
            {
                if (async) await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                else _container.CreateIfNotExists(PublicAccessType.None, cancellationToken: cancellationToken);
            }
            var properties = async ? (await _container.GetPropertiesAsync(cancellationToken: cancellationToken)).Value : _container.GetProperties(cancellationToken: cancellationToken).Value;
            // Group projections are private: never serve them from a public container.
            if (properties.PublicAccess is { } access && access != PublicAccessType.None)
                throw new InvalidOperationException("The distributed cache container must be private.");
            _initialized = true;
        }
        finally { _initialization.Release(); }
    }

    private sealed class BlobLease : IDistributedCacheLease
    {
        private readonly BlobLeaseClient _lease;
        private readonly CancellationTokenSource _stop = new();
        private readonly CancellationTokenSource _lost = new();
        private readonly Task _renewal;
        private readonly ILogger _logger;
        public string Key { get; }
        public CancellationToken LeaseLost => _lost.Token;

        public BlobLease(string key, BlobLeaseClient lease, TimeSpan renewal, ILogger logger)
        {
            Key = key; _lease = lease; _logger = logger;
            _renewal = RenewAsync(renewal);
        }

        private async Task RenewAsync(TimeSpan interval)
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await _lease.RenewAsync(cancellationToken: timeout.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception)
            {
                _logger.LogWarning("Distributed cache lease renewal failed; the holder must stop publishing.");
                await _lost.CancelAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            try { await _renewal; } catch { }
            try { await _lease.ReleaseAsync(); }
            catch (RequestFailedException) { } // Expired or already released: it frees itself.
            finally { _stop.Dispose(); _lost.Dispose(); }
        }
    }
}
