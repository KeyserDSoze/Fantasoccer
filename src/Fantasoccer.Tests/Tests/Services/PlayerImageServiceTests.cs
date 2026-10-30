using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Business;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

// The photo matcher/downloader stays covered without Content, repository factories or legacy background jobs.
public sealed class PlayerImageServiceTests
{
    private sealed class Photos : IPlayerImageStore
    {
        public Dictionary<string, byte[]> Files { get; } = [];
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

    private static (ServiceProvider Services, IPlayerImageRefresher Refresher, Photos Store) Setup(RealPlayer player, Source source)
    {
        var services = new ServiceCollection();
        services.AddFantasoccerSqlDataProviders();
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
        var setup = Setup(player, new("Role Match Player", officialRole)); await using var services = setup.Services;
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
        var setup = Setup(player, new(player.Name, 2) { ContentType = mime }); await using var services = setup.Services;
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
}
