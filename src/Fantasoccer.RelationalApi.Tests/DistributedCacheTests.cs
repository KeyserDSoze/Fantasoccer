using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Fantasoccer.Application.Core.Caching;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Caching;
using Fantasoccer.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

// Runs every behavioural test against the real Azure Blob provider (Azurite) and
// against the in-memory double used by the API test host, so both stay equivalent.
public sealed class DistributedCacheTests : IAsyncLifetime
{
    private const string StorageVariable = "FANTASOCCER_TEST_STORAGE";
    private readonly string _container = "cache-" + Guid.NewGuid().ToString("N");
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly List<InMemoryCoordinatedDistributedCache> _memory = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync()
    {
        if (Storage() is { } connection) await new BlobContainerClient(connection, _container).DeleteIfExistsAsync();
    }

    private static string? Storage()
    {
        var connection = Environment.GetEnvironmentVariable(StorageVariable);
        if (string.IsNullOrWhiteSpace(connection)) return null;
        // Dedicated local emulator account only.
        if (!connection.Contains("AccountName=migrationtest;", StringComparison.Ordinal) || !connection.Contains("127.0.0.1", StringComparison.Ordinal) && !connection.Contains("azurite", StringComparison.Ordinal))
            throw new InvalidOperationException("Cache tests only use the local migrationtest Azurite account.");
        return connection;
    }

    // A second instance of the same provider type = a second API instance.
    private (IDistributedCache Cache, IDistributedCacheCoordinator Coordinator) Provider(string kind, string @namespace = "test", InMemoryCoordinatedDistributedCache? shared = null)
    {
        if (kind == "memory")
        {
            var memory = shared ?? new InMemoryCoordinatedDistributedCache(_clock);
            _memory.Add(memory);
            return (memory, memory);
        }
        var connection = Storage();
        if (connection is null) Assert.Skip("Set " + StorageVariable + " (scripts/migration/test-local.sh) to run the Azurite cache tests.");
        var blob = new AzureBlobDistributedCache(new BlobContainerClient(connection, _container),
            new AzureBlobDistributedCacheOptions { ContainerName = _container, Namespace = @namespace }, _clock);
        return (blob, blob);
    }

    private BlobContainerClient Container() => new(Storage()!, _container);

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Sync_and_async_get_set_refresh_remove_follow_the_IDistributedCache_contract(string kind)
    {
        var (cache, _) = Provider(kind);
        Assert.Null(await cache.GetAsync("tests:missing"));
        Assert.Null(cache.Get("tests:missing"));
        cache.Set("tests:sync", "{\"a\":1}"u8.ToArray(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
        await cache.SetAsync("tests:async", "{\"b\":2}"u8.ToArray(), new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
        Assert.Equal("{\"a\":1}", Encoding.UTF8.GetString(cache.Get("tests:sync")!));
        Assert.Equal("{\"b\":2}", Encoding.UTF8.GetString((await cache.GetAsync("tests:async"))!));
        cache.Refresh("tests:async");
        await cache.RefreshAsync("tests:async");
        await cache.RefreshAsync("tests:missing");
        cache.Remove("tests:sync");
        await cache.RemoveAsync("tests:async");
        await cache.RemoveAsync("tests:never-existed");
        Assert.Null(cache.Get("tests:sync"));
        Assert.Null(await cache.GetAsync("tests:async"));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Set("tests:past", [1], new DistributedCacheEntryOptions { AbsoluteExpiration = _clock.GetUtcNow().AddSeconds(-1) }));
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Absolute_and_relative_expiration_are_logical_even_if_the_blob_still_exists(string kind)
    {
        var (cache, coordinator) = Provider(kind);
        await cache.SetAsync("tests:absolute", [1, 2, 3], new DistributedCacheEntryOptions { AbsoluteExpiration = _clock.GetUtcNow().AddMinutes(10) });
        await cache.SetAsync("tests:relative", [4], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) });
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(await cache.GetAsync("tests:absolute"));
        Assert.NotNull(await cache.GetAsync("tests:relative"));
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await cache.GetAsync("tests:relative"));
        Assert.NotNull(await coordinator.GetEntryAsync("tests:absolute"));
        _clock.Advance(TimeSpan.FromMinutes(8));
        Assert.Null(await cache.GetAsync("tests:absolute"));
        if (kind == "blob")
            Assert.True(await Container().GetBlobClient(CacheBlobNaming.BlobName("test", "tests:absolute")).ExistsAsync());
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Sliding_expiration_is_extended_by_reads_and_refresh_but_capped_by_absolute(string kind)
    {
        var (cache, coordinator) = Provider(kind);
        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(25) };
        await cache.SetAsync("tests:sliding", [7], options);
        for (var i = 0; i < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(6));
            Assert.NotNull(await cache.GetAsync("tests:sliding"));
        }
        _clock.Advance(TimeSpan.FromMinutes(6));
        cache.Refresh("tests:sliding");
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.NotNull(await cache.GetAsync("tests:sliding")); // 24 minutes, within the absolute cap
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await cache.GetAsync("tests:sliding")); // absolute cap reached despite recent access
        await cache.SetAsync("tests:idle", [8], new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });
        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Null(await coordinator.GetEntryAsync("tests:idle"));
    }

    [Fact]
    public async Task Sliding_reads_do_not_write_the_blob_on_every_access()
    {
        var (cache, coordinator) = Provider("blob");
        await cache.SetAsync("tests:hot", "{}"u8.ToArray(), new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });
        var first = (await coordinator.GetEntryAsync("tests:hot"))!.ETag;
        _clock.Advance(TimeSpan.FromSeconds(30)); // below 10% of the window
        Assert.Equal(first, (await coordinator.GetEntryAsync("tests:hot"))!.ETag);
        Assert.Equal(first, (await coordinator.GetEntryAsync("tests:hot"))!.ETag);
        _clock.Advance(TimeSpan.FromMinutes(2)); // past the refresh threshold: one metadata write
        var touched = (await coordinator.GetEntryAsync("tests:hot"))!.ETag;
        Assert.NotEqual(first, touched);
        Assert.Equal(touched, (await coordinator.GetEntryAsync("tests:hot"))!.ETag);
    }

    [Fact]
    public async Task Blob_documents_are_private_UTF8_JSON_with_expiration_metadata_and_ordered_paths()
    {
        var (_, coordinator) = Provider("blob", "prod");
        var store = Shared(coordinator);
        var data = new FootballLiveDataStore(store, _clock);
        await data.PublishChancesAsync(_clock.GetUtcNow(), new(15, 8, ["Fantagazzetta"],
            [new("rossi", "Rossi", "mil", "Milan", 3, true, true, true, false, false, false, 1, "Infortunio muscolare")]));
        var container = Container();
        Assert.Equal(PublicAccessType.None, (await container.GetPropertiesAsync()).Value.PublicAccess ?? PublicAccessType.None);
        var blob = container.GetBlobClient("prod/football/v1/15/day-08/chances.json");
        var download = (await blob.DownloadContentAsync()).Value;
        Assert.StartsWith("application/json", download.Details.ContentType, StringComparison.Ordinal);
        Assert.True(download.Details.Metadata.ContainsKey("expiresat"));
        using var json = JsonDocument.Parse(download.Content.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("football-chances", json.RootElement.GetProperty("kind").GetString());
        Assert.True(json.RootElement.TryGetProperty("generatedAtUtc", out _));
        Assert.True(json.RootElement.TryGetProperty("freshUntilUtc", out _));
        Assert.Equal("rossi", json.RootElement.GetProperty("payload").GetProperty("players")[0].GetProperty("playerId").GetString());
        var read = await data.ChancesAsync(15, 8);
        Assert.Equal("Infortunio muscolare", read!.Payload.Players.Single().Description);
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Concurrent_conditional_writes_from_two_instances_have_exactly_one_winner(string kind)
    {
        var first = Provider(kind);
        var second = Provider(kind, shared: kind == "memory" ? _memory[0] : null);
        var created = await first.Coordinator.TrySetAsync("tests:cas", "{\"v\":0}"u8.ToArray(), OneHour(), null);
        Assert.NotNull(created);
        Assert.Null(await second.Coordinator.TrySetAsync("tests:cas", "{\"v\":9}"u8.ToArray(), OneHour(), null)); // already present
        var results = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            (i % 2 == 0 ? first : second).Coordinator.TrySetAsync("tests:cas", Encoding.UTF8.GetBytes("{\"v\":" + i + "}"), OneHour(), created)));
        Assert.Single(results, x => x is not null);
        Assert.Null(await first.Coordinator.TrySetAsync("tests:cas", "{\"v\":99}"u8.ToArray(), OneHour(), created)); // stale ETag refused
        // An expired entry counts as absent for create-only writes.
        await first.Cache.SetAsync("tests:expired", [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(await second.Coordinator.TrySetAsync("tests:expired", [2], OneHour(), null));
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Leases_are_exclusive_non_blocking_and_released(string kind)
    {
        var first = Provider(kind);
        var second = Provider(kind, shared: kind == "memory" ? _memory[0] : null);
        var lease = await first.Coordinator.TryAcquireLeaseAsync("tests:regenerate", TimeSpan.FromSeconds(15));
        Assert.NotNull(lease);
        Assert.Null(await second.Coordinator.TryAcquireLeaseAsync("tests:regenerate", TimeSpan.FromSeconds(15)));
        Assert.NotNull(await second.Coordinator.TryAcquireLeaseAsync("tests:other", TimeSpan.FromSeconds(15)));
        await lease!.DisposeAsync();
        var again = await second.Coordinator.TryAcquireLeaseAsync("tests:regenerate", TimeSpan.FromSeconds(15));
        Assert.NotNull(again);
        await again!.DisposeAsync();
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Publication_is_monotonic_idempotent_and_merges_recovery_scopes(string kind)
    {
        var (_, coordinator) = Provider(kind);
        var data = new FootballLiveDataStore(Shared(coordinator), _clock);
        var newer = _clock.GetUtcNow();
        var older = newer.AddMinutes(-1);
        CachedLiveVote Vote(string id, string club, float value) => new(id, id, club, club, 1, true, true, new(1, value, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false));
        Assert.Equal(CachePublishOutcome.Published, await data.PublishLiveVotesAsync(newer, new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 6)]), null));
        // A slower producer that fetched earlier cannot overwrite the newer snapshot.
        Assert.Equal(CachePublishOutcome.SkippedNewerExists, await data.PublishLiveVotesAsync(older, new(15, 8, [Vote("a", "mil", 1)]), null));
        Assert.Equal(CachePublishOutcome.Unchanged, await data.PublishLiveVotesAsync(newer, new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 6)]), null));
        // Recovery refresh: only the scoped club is replaced.
        Assert.Equal(CachePublishOutcome.Published, await data.PublishLiveVotesAsync(newer.AddMinutes(1), new(15, 8, [Vote("b", "int", 8)]), new HashSet<string> { "int" }));
        var stored = (await data.LiveVotesAsync(15, 8))!.Payload.Players;
        Assert.Equal(6, stored.Single(x => x.PlayerId == "a").Vote.Value);
        Assert.Equal(8, stored.Single(x => x.PlayerId == "b").Vote.Value);
        await Assert.ThrowsAsync<ArgumentException>(() => data.PublishLiveVotesAsync(newer.AddMinutes(2), new(15, 8, []), null));
        Assert.Equal(2, (await data.LiveVotesAsync(15, 8))!.Payload.Players.Length);
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Identical_snapshots_are_not_rewritten_but_reconfirmed_on_a_heartbeat_and_changed_content_is_published(string kind)
    {
        var (_, coordinator) = Provider(kind);
        var data = new FootballLiveDataStore(Shared(coordinator), _clock);
        CachedLiveVote Vote(string id, string club, float value) => new(id, id, club, club, 1, true, true, new(1, value, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false));
        var key = CacheKeys.FootballLiveVotes(15, 8);
        var t0 = _clock.GetUtcNow();
        Assert.Equal(CachePublishOutcome.Published, await data.PublishLiveVotesAsync(t0, new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 6)]), null));
        var written = (await coordinator.GetEntryAsync(key))!;
        var fingerprint = (await data.LiveVotesAsync(15, 8))!.SourceFingerprint;
        Assert.False(string.IsNullOrEmpty(fingerprint));

        // The job runs every minute: the same votes fetched later must not touch the document.
        foreach (var minute in new[] { 1, 2, 4 })
            Assert.Equal(CachePublishOutcome.Unchanged, await data.PublishLiveVotesAsync(t0.AddMinutes(minute), new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 6)]), null));
        Assert.Equal(written.ETag, (await coordinator.GetEntryAsync(key))!.ETag);

        // After the heartbeat the same content is re-confirmed: freshness moves, the content identity does not.
        Assert.Equal(CachePublishOutcome.Published, await data.PublishLiveVotesAsync(t0.AddMinutes(6), new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 6)]), null));
        var confirmed = (await data.LiveVotesAsync(15, 8))!;
        Assert.Equal(fingerprint, confirmed.SourceFingerprint);
        Assert.Equal(t0.AddMinutes(6), confirmed.GeneratedAtUtc);
        Assert.NotEqual(written.ETag, (await coordinator.GetEntryAsync(key))!.ETag);

        // Any change is published at once, whatever the heartbeat says.
        Assert.Equal(CachePublishOutcome.Published, await data.PublishLiveVotesAsync(t0.AddMinutes(7), new(15, 8, [Vote("a", "mil", 6), Vote("b", "int", 7)]), null));
        Assert.NotEqual(fingerprint, (await data.LiveVotesAsync(15, 8))!.SourceFingerprint);

        // A recovery refresh that changes nothing in the stored content leaves the document alone too.
        var before = (await coordinator.GetEntryAsync(key))!.ETag;
        Assert.Equal(CachePublishOutcome.Unchanged, await data.PublishLiveVotesAsync(t0.AddMinutes(8), new(15, 8, [Vote("b", "int", 7)]), new HashSet<string> { "int" }));
        Assert.Equal(before, (await coordinator.GetEntryAsync(key))!.ETag);
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Snapshot_freshness_is_explicit_and_probable_lineups_that_were_never_confirmed_for_days_are_missing(string kind)
    {
        var (_, coordinator) = Provider(kind);
        var data = new FootballLiveDataStore(Shared(coordinator), _clock);
        var t0 = _clock.GetUtcNow();
        await data.PublishChancesAsync(t0, new(15, 8, ["Fantagazzetta"], [new("rossi", "Rossi", "mil", "Milan", 3, true, true, true, false, false, false, 1, null)]));
        await data.PublishLiveVotesAsync(t0, new(15, 8, [new("a", "a", "mil", "Milan", 1, true, true, new(1, 6, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false))]), null);

        var chances = (await data.ChancesAsync(15, 8))!;
        Assert.True(chances.IsFreshAt(t0.AddHours(11)));
        Assert.False(chances.IsFreshAt(t0.AddHours(13)));
        var votes = (await data.LiveVotesAsync(15, 8))!;
        Assert.True(votes.IsFreshAt(t0.AddMinutes(9)));
        Assert.False(votes.IsFreshAt(t0.AddMinutes(11)));

        // Not updated, but still the last known data: usable and flagged by the callers.
        _clock.Advance(TimeSpan.FromHours(13));
        Assert.NotNull(await data.ChancesAsync(15, 8));
        Assert.NotNull(await data.LiveVotesAsync(15, 8));
        // A past matchday is no longer refreshed, yet keeps its last known lineups for 30 days.
        _clock.Advance(TimeSpan.FromDays(29));
        Assert.NotNull(await data.ChancesAsync(15, 8));
        // Probable lineups nobody confirmed for more than 30 days are treated as missing even though the
        // document still exists: the physical retention must outlive the usable window.
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.NotNull(await coordinator.GetEntryAsync(CacheKeys.FootballChances(15, 8)));
        Assert.Null(await data.ChancesAsync(15, 8));
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Purge_reaches_expired_entries_whatever_precedes_them_and_limits_only_the_deletions(string kind)
    {
        var (cache, coordinator) = Provider(kind);
        // Valid entries sort first: a scan capped at the first N blobs would never get past them.
        for (var index = 0; index < 6; index++)
            await cache.SetAsync($"tests:a{index}", [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(10) });
        for (var index = 0; index < 3; index++)
            await cache.SetAsync($"tests:z{index}", [2], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
        _clock.Advance(TimeSpan.FromDays(3));
        var first = await coordinator.PurgeExpiredAsync(TimeSpan.FromDays(1), 2);
        Assert.Equal(2, first.Deleted);
        var second = await coordinator.PurgeExpiredAsync(TimeSpan.FromDays(1), 2);
        Assert.Equal(1, second.Deleted);
        Assert.Equal(0, (await coordinator.PurgeExpiredAsync(TimeSpan.FromDays(1), 2)).Deleted);
        for (var index = 0; index < 6; index++) Assert.NotNull(await cache.GetAsync($"tests:a{index}"));
        Assert.Equal(0, (await coordinator.PurgeExpiredAsync(TimeSpan.FromDays(1), 0)).Deleted);
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Missing_corrupted_or_foreign_documents_are_cache_misses(string kind)
    {
        var (cache, coordinator) = Provider(kind);
        var shared = Shared(coordinator);
        Assert.Null(await shared.ReadAsync<FootballChancesPayload>("football:v1:15:day-01:chances", "football-chances", 1));
        await cache.SetAsync("football:v1:15:day-02:chances", "not json"u8.ToArray(), OneHour());
        Assert.Null(await shared.ReadAsync<FootballChancesPayload>("football:v1:15:day-02:chances", "football-chances", 1));
        await cache.SetAsync("football:v1:15:day-03:chances", JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 99, kind = "football-chances" }), OneHour());
        Assert.Null(await shared.ReadAsync<FootballChancesPayload>("football:v1:15:day-03:chances", "football-chances", 1));
        if (kind != "blob") return;
        // A blob written without provider metadata (or tampered) is never trusted.
        await Container().GetBlobClient(CacheBlobNaming.BlobName("test", "tests:raw")).UploadAsync(BinaryData.FromString("{}"), overwrite: true);
        Assert.Null(await cache.GetAsync("tests:raw"));
    }

    [Fact]
    public void Keys_are_deterministic_versioned_and_isolated_by_environment_group_season_and_day()
    {
        var group = Guid.Parse("4c2d1f8e-1111-4a3b-9c7d-000000000001");
        var other = Guid.Parse("4c2d1f8e-1111-4a3b-9c7d-000000000002");
        Assert.Equal("prod/football/v1/15/day-08/chances.json", CacheBlobNaming.BlobName("prod", CacheKeys.FootballChances(15, 8)));
        Assert.Equal("prod/football/v1/15/day-08/live-votes.json", CacheBlobNaming.BlobName("prod", CacheKeys.FootballLiveVotes(15, 8)));
        Assert.Equal($"prod/live/v1/{group:N}/15/day-08.json", CacheBlobNaming.BlobName("prod", CacheKeys.LiveGroup(group, 15, 8)));
        Assert.DoesNotContain(group.ToString("N"), CacheKeys.FootballChances(15, 8));
        var names = new[] { CacheKeys.LiveGroup(group, 15, 8), CacheKeys.LiveGroup(other, 15, 8), CacheKeys.LiveGroup(group, 16, 8), CacheKeys.LiveGroup(group, 15, 9) }
            .SelectMany(key => new[] { CacheBlobNaming.BlobName("prod", key), CacheBlobNaming.BlobName("staging", key) }).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        // Unsafe keys can never escape the namespace prefix.
        var hashed = CacheBlobNaming.BlobName("prod", "../../other/../secret:x");
        Assert.StartsWith("prod/_hashed/", hashed, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => CacheBlobNaming.BlobName("../prod", "x"));
    }

    [Fact]
    public async Task Namespaces_sharing_one_container_do_not_see_each_other()
    {
        var prod = Provider("blob", "prod");
        var staging = Provider("blob", "staging");
        await prod.Cache.SetAsync("tests:same", "{\"env\":\"prod\"}"u8.ToArray(), OneHour());
        Assert.Null(await staging.Cache.GetAsync("tests:same"));
        await staging.Cache.SetAsync("tests:same", "{\"env\":\"staging\"}"u8.ToArray(), OneHour());
        Assert.Equal("{\"env\":\"prod\"}", Encoding.UTF8.GetString((await prod.Cache.GetAsync("tests:same"))!));
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Purge_removes_only_entries_expired_beyond_the_grace_period(string kind)
    {
        var (cache, coordinator) = Provider(kind);
        await cache.SetAsync("tests:old", [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
        await cache.SetAsync("tests:recent", [2], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(30) });
        await cache.SetAsync("tests:forever", [3], new DistributedCacheEntryOptions());
        _clock.Advance(TimeSpan.FromHours(28));
        var result = await coordinator.PurgeExpiredAsync(TimeSpan.FromDays(1), 1000);
        Assert.Equal(1, result.Deleted);
        Assert.NotNull(await cache.GetAsync("tests:recent"));
        Assert.NotNull(await cache.GetAsync("tests:forever"));
        if (kind == "blob")
            Assert.False(await Container().GetBlobClient(CacheBlobNaming.BlobName("test", "tests:old")).ExistsAsync());
    }

    [Theory, InlineData("memory"), InlineData("blob")]
    public async Task Stale_while_revalidate_serves_flagged_stale_data_only_while_another_instance_regenerates(string kind)
    {
        var (_, coordinator) = Provider(kind);
        var shared = Shared(coordinator);
        var key = "live:v1:00000000000000000000000000000001:15:day-08";
        var regenerations = 0;
        Task<CacheEnvelope<string>> Build(string fingerprint) { regenerations++; return Task.FromResult(new CacheEnvelope<string>(1, "live-group", _clock.GetUtcNow(),
            _clock.GetUtcNow().AddMinutes(1), _clock.GetUtcNow().UtcTicks, fingerprint, "payload-" + fingerprint)); }
        var first = await shared.GetOrRegenerateAsync(key, "live-group", 1, x => x.SourceFingerprint == "a", _ => Build("a"), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
        Assert.False(first.IsStale);
        var hit = await Shared(coordinator).GetOrRegenerateAsync(key, "live-group", 1, x => x.SourceFingerprint == "a", _ => Build("a"), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
        Assert.Equal(1, regenerations); // second instance reuses the shared projection
        Assert.Equal("payload-a", hit.Envelope.Payload);
        // Another instance holds the regeneration lease: the previous projection is served, flagged.
        await using (await coordinator.TryAcquireLeaseAsync(CacheKeys.RegenerationLock(key), TimeSpan.FromSeconds(30)))
        {
            _clock.Advance(TimeSpan.FromSeconds(10));
            var stale = await Shared(coordinator).GetOrRegenerateAsync(key, "live-group", 1, x => x.SourceFingerprint == "b", _ => Build("b"), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
            Assert.True(stale.IsStale);
            Assert.Equal("payload-a", stale.Envelope.Payload);
            Assert.Equal(1, regenerations);
        }
        var fresh = await Shared(coordinator).GetOrRegenerateAsync(key, "live-group", 1, x => x.SourceFingerprint == "b", _ => Build("b"), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
        Assert.False(fresh.IsStale);
        Assert.Equal("payload-b", fresh.Envelope.Payload);
        // Cache loss: the projection is rebuilt from the source, never lost data.
        await ((IDistributedCache)(object)coordinator).RemoveAsync(key);
        var rebuilt = await Shared(coordinator).GetOrRegenerateAsync(key, "live-group", 1, x => x.SourceFingerprint == "b", _ => Build("b"), TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));
        Assert.Equal("payload-b", rebuilt.Envelope.Payload);
        Assert.Equal(3, regenerations);
    }

    private SharedSnapshotCache Shared(IDistributedCacheCoordinator coordinator) => new(coordinator, new MemoryCache(new MemoryCacheOptions()), _clock,
        Options.Create(new SharedCacheOptions { RegenerationWait = TimeSpan.FromMilliseconds(300) }), NullLogger<SharedSnapshotCache>.Instance);
    private static DistributedCacheEntryOptions OneHour() => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };
}
