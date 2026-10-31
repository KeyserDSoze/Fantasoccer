using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Fantasoccer.Business;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

// The photo matcher/downloader stays covered without Content, repository factories or legacy background jobs.
public sealed class PlayerImageServiceTests
{
    private sealed class Photos : IPlayerImageStore
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Files.ContainsKey(name));
        public Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken)
        { Files[name] = image.ToArray(); return Task.CompletedTask; }
    }

    private sealed class Catalogue(RealPlayer player) : IFootballIngestionReader
    {
        public Task<RealCalendar?> CalendarAsync(int year) => Task.FromResult<RealCalendar?>(null);
        public Task<RealPlayersWrapper?> PlayersAsync(int year) => Task.FromResult<RealPlayersWrapper?>(new() { Players = [player] });
        public Task<RealTeamWrapper?> ClubsAsync(int year) => Task.FromResult<RealTeamWrapper?>(null);
    }

    private sealed class Source(string name, int role) : HttpMessageHandler
    {
        public bool PreviousOnly { get; set; }
        public string Path { get; set; } = "playerImages/photo.webp";
        public string ContentType { get; set; } = "image/webp";
        public byte[] Bytes { get; set; } = [1, 2, 3];
        public List<Uri> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Calls.Add(uri);
            HttpResponseMessage response;
            if (uri.AbsolutePath.Contains("/competitions/"))
                response = new(HttpStatusCode.OK) { Content = JsonContent.Create(new { seasons = new[]
                {
                    new { seasonId = "current", seasonName = FantacalcioTime.LabelYear(FantacalcioTime.ActualYear) },
                    new { seasonId = "previous", seasonName = FantacalcioTime.LabelYear(FantacalcioTime.ActualYear - 1) }
                } }) };
            else if (uri.Host == "media-sdp.legaseriea.it")
            {
                response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) };
                response.Content.Headers.ContentType = new(ContentType);
            }
            else
            {
                var previous = uri.AbsolutePath.Contains("/previous/");
                response = new(HttpStatusCode.OK) { Content = JsonContent.Create(new
                {
                    pagination = new { totalPages = 1, currentPage = 1, isLastPage = true },
                    players = new[] { new { displayName = name, shortName = name, role,
                        team = new { shortName = "Milan", officialName = "Milan", acronymName = "MIL" },
                        imagery = new { playerImage_home_middle = PreviousOnly && !previous ? null : Path } } }
                }) };
            }
            return Task.FromResult(response);
        }
    }

    private static (ServiceProvider Services, IPlayerImageRefresher Refresher, Photos Store) Setup(RealPlayer player, HttpMessageHandler source,
        Action<PlayerImageRefreshOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddFantasoccerSqlDataProviders();
        if (configure is not null) services.Configure(configure);
        services.AddLock();
        services.AddSingleton<IFootballIngestionReader>(new Catalogue(player));
        var photos = new Photos();
        services.AddSingleton<IPlayerImageStore>(photos);
        services.AddHttpClient("player-images").ConfigurePrimaryHttpMessageHandler(() => source);
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<IPlayerImageRefresher>(), photos);
    }

    private static RealPlayer Player(string name, Role role) => new() { Name = name, Role = role, RealTeam = new() { Name = "Milan" }, IsActive = true };

    [Theory]
    [InlineData("Floriani Mussolini", "Romano Floriani Mussolini")]
    [InlineData("Cissè A.", "Alphadjo Cisse")]
    [InlineData("Rahmani", "Amir Rrahmani")]
    [InlineData("Mario Rossi", "Mario Rossi")]
    public async Task Matcher_keeps_name_normalization_and_photo_reload_uses_the_same_blob_key(string rosterName, string officialName)
    {
        var player = Player(rosterName, Role.Defensor);
        var source = new Source(officialName, 2);
        var setup = Setup(player, source); await using var services = setup.Services;
        var key = $"players/{player.Key}.jpg";
        setup.Store.Files[key] = [9];
        Assert.True(await setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(source.Bytes, setup.Store.Files[key]);
        source.Bytes = [4, 5];
        Assert.True(await setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(source.Bytes, setup.Store.Files[key]);
        Assert.Equal(2, source.Calls.Count(x => x.Host == "media-sdp.legaseriea.it"));
        Assert.DoesNotContain(source.Calls, x => x.AbsolutePath.Contains("/previous/"));
    }

    [Theory]
    [InlineData(Role.Defensor, 2, true)]
    [InlineData(Role.Defensor, 3, true)]
    [InlineData(Role.Defensor, 4, false)]
    [InlineData(Role.Midfielder, 2, true)]
    [InlineData(Role.Midfielder, 3, true)]
    [InlineData(Role.Midfielder, 4, true)]
    [InlineData(Role.Midfielder, 1, false)]
    [InlineData(Role.Forward, 3, true)]
    [InlineData(Role.Forward, 4, true)]
    [InlineData(Role.Forward, 2, false)]
    [InlineData(Role.GoalKeeper, 1, true)]
    [InlineData(Role.GoalKeeper, 2, false)]
    public async Task Adjacent_roles_are_allowed_but_goalkeepers_are_kept_separate(Role role, int officialRole, bool expected)
    {
        var player = Player("Role Match Player", role);
        var setup = Setup(player, new Source("Role Match Player", officialRole)); await using var services = setup.Services;
        Assert.Equal(expected, await setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Scheduled_refresh_falls_back_to_the_previous_season_and_does_not_replace_existing_photos()
    {
        var player = Player("Mario Rossi", Role.Defensor);
        var source = new Source(player.Name, 2) { PreviousOnly = true };
        var setup = Setup(player, source); await using var services = setup.Services;
        await setup.Refresher.DownloadMissingAsync(TestContext.Current.CancellationToken);
        Assert.True(setup.Store.Files.ContainsKey($"players/{player.Key}.jpg"));
        Assert.Contains(source.Calls, x => x.AbsolutePath.Contains("/previous/"));
        source.Bytes = [9];
        await setup.Refresher.DownloadMissingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 1, 2, 3 }, setup.Store.Files[$"players/{player.Key}.jpg"]);
        Assert.Single(source.Calls, x => x.Host == "media-sdp.legaseriea.it");
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("binary/octet-stream")]
    public async Task Invalid_source_content_preserves_the_existing_photo(string mime)
    {
        var player = Player("Mario Rossi", Role.Defensor);
        var setup = Setup(player, new Source(player.Name, 2) { ContentType = mime }); await using var services = setup.Services;
        var key = $"players/{player.Key}.jpg";
        setup.Store.Files[key] = [9];
        await Assert.ThrowsAsync<InvalidDataException>(() => setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 9 }, setup.Store.Files[key]);
    }

    [Fact]
    public async Task Photo_urls_outside_the_official_host_are_rejected_without_fetching_them()
    {
        var player = Player("Mario Rossi", Role.Defensor);
        var source = new Source(player.Name, 2) { Path = "http://127.0.0.1/private" };
        var setup = Setup(player, source); await using var services = setup.Services;
        await Assert.ThrowsAsync<InvalidDataException>(() => setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(source.Calls, x => x.Host == "127.0.0.1");
        Assert.Empty(setup.Store.Files);
    }

    [Fact]
    public async Task Catalogue_pages_are_loaded_with_bounded_parallelism_and_a_failed_page_preserves_the_old_photo()
    {
        var player = Player("Paged Photo Player", Role.Defensor);
        var source = new PagedSource(player.Name);
        var setup = Setup(player, source); await using var services = setup.Services;
        Assert.True(await setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.InRange(source.MaximumConcurrent, 2, 4);
        Assert.Equal(9, source.PagesRead);
        Assert.True(setup.Store.Files.ContainsKey($"players/{player.Key}.jpg"));
        setup.Store.Files[$"players/{player.Key}.jpg"] = [9];
        source.FailPage = 3;
        await Assert.ThrowsAsync<HttpRequestException>(() => setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 9 }, setup.Store.Files[$"players/{player.Key}.jpg"]);
        source.FailPage = null; source.IncompletePage = 3;
        await Assert.ThrowsAsync<InvalidDataException>(() => setup.Refresher.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 9 }, setup.Store.Files[$"players/{player.Key}.jpg"]);
    }

    private sealed class PagedSource(string name) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private int _active;
        public int MaximumConcurrent { get; private set; }
        public int PagesRead { get; private set; }
        public int? FailPage { get; set; }
        public int? IncompletePage { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("/competitions/")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                seasons = new[] { new { seasonId = "current", seasonName = FantacalcioTime.LabelYear(FantacalcioTime.ActualYear) } }
            }) };
            if (request.RequestUri.Host == "media-sdp.legaseriea.it")
            {
                var image = new ByteArrayContent([1, 2, 3]); image.Headers.ContentType = new("image/webp");
                return new(HttpStatusCode.OK) { Content = image };
            }
            var page = int.Parse(request.RequestUri.Query.TrimStart('?').Split('&').Single(x => x.StartsWith("page=")).Split('=')[1]);
            lock (_gate) { PagesRead++; _active++; MaximumConcurrent = Math.Max(MaximumConcurrent, _active); }
            try
            {
                await Task.Delay(20, cancellationToken);
                if (page == FailPage) return new(HttpStatusCode.ServiceUnavailable);
                if (page == IncompletePage) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
                { pagination = new { totalPages = 9, currentPage = page, isLastPage = page == 9 }, players = (object?)null }) };
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
                {
                    pagination = new { totalPages = 9, currentPage = page, isLastPage = page == 9 },
                    players = page == 9 ? new[] { new { displayName = name, shortName = name, role = 2,
                        team = new { officialName = "Milan" }, imagery = new { playerImage_home_middle = "playerImages/photo.webp" } } } : []
                }) };
            }
            finally { lock (_gate) _active--; }
        }
    }

    [Fact]
    public async Task Distinct_authoritative_players_with_the_same_name_get_distinct_files_and_complete_results()
    {
        var first = Player("Same Photo Player", Role.Defensor); first.PlayerId = "samephotofirst";
        var second = Player("Same Photo Player", Role.Defensor); second.PlayerId = "samephotosecond";
        var setup = Setup(first, new Source(first.Name, 2)); await using var services = setup.Services;
        var result = await setup.Refresher.ReloadManyAsync([first, second], TestContext.Current.CancellationToken);
        Assert.Equal(new[] { first.PlayerId, second.PlayerId }, result.Select(x => x.PlayerId));
        Assert.All(result, x => Assert.True(x.Succeeded));
        Assert.True(setup.Store.Files.ContainsKey("players/samephotofirst.jpg"));
        Assert.True(setup.Store.Files.ContainsKey("players/samephotosecond.jpg"));
        Assert.False(setup.Store.Files.ContainsKey($"players/{first.Key}.jpg"));
    }

    [Fact]
    public async Task Whole_team_refresh_has_one_overall_budget_and_returns_successes_and_unfinished_IDs_for_retry()
    {
        var players = Enumerable.Range(0, 25).Select(index => { var p = Player("Budget Photo Player", Role.Defensor); p.PlayerId = "budgetphoto" + (char)('a' + index); return p; }).ToArray();
        var setup = Setup(players[0], new BudgetSource(players[0].Name), options =>
        { options.BatchTimeout = TimeSpan.FromSeconds(1); options.ImageTimeout = TimeSpan.FromSeconds(1); options.ParallelDownloads = 1; });
        await using var services = setup.Services;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var result = await setup.Refresher.ReloadManyAsync(players, TestContext.Current.CancellationToken);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal(25, result.Count);
        Assert.Single(result, x => x.Succeeded);
        Assert.Equal(24, result.Count(x => !x.Succeeded));
        Assert.All(result.Where(x => !x.Succeeded), x => Assert.Contains("Riprova", x.Error));
        Assert.Equal(players.Select(x => x.PlayerId), result.Select(x => x.PlayerId));
    }

    private sealed class BudgetSource(string name) : HttpMessageHandler
    {
        private readonly Source _catalogue = new(name, 2);
        private int _images;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "media-sdp.legaseriea.it" && Interlocked.Increment(ref _images) > 1)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            using var inner = new HttpMessageInvoker(_catalogue, disposeHandler: false);
            return await inner.SendAsync(request, cancellationToken);
        }
    }
}
