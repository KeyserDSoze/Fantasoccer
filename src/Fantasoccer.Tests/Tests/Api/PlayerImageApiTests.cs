using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Tests.Tests.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class PlayerImageApiTests : IAsyncLifetime
{
    private const string Owner = "image-refresh@test.local";
    private const string GroupId = "image-refresh-group";
    private readonly HttpClient _client;
    private readonly IRepository<AppIdentity, string> _users;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private readonly IContentRepository _images;
    private readonly TestPlayerImagesHandler _handler;
    private RealPlayersWrapper? _snapshot;
    private readonly RealPlayer _player = new() { Name = "API Image Player", Role = Role.Forward, RealTeam = new RealTeam { Name = "Roma" }, IsActive = true };

    public PlayerImageApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _users = factory.Services.GetRequiredService<IRepository<AppIdentity, string>>();
        _players = factory.Services.GetRequiredService<IRepository<RealPlayersWrapper, int>>();
        _images = factory.Services.GetRequiredService<IFactory<IContentRepository>>().Create("images")!;
        _handler = factory.Services.GetRequiredService<TestPlayerImagesHandler>();
    }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _snapshot = await _players.GetAsync(FantacalcioTime.ActualYear, ct);
        await _players.DeleteAsync(FantacalcioTime.ActualYear, ct);
        await _players.InsertAsync(FantacalcioTime.ActualYear, new RealPlayersWrapper { Players = [_player] }, ct);
        await _users.InsertAsync(Owner, new AppIdentity
        {
            Email = Owner, Username = "Image owner",
            Groups = [new GroupForIdentity { Id = GroupId, Name = "Image group", Role = IdentityRole.Participant }]
        }, ct);
        await _images.UploadAsync($"players/{_player.Key}.jpg", [9], overwrite: true, cancellationToken: ct);
        _handler.Requests.Clear();
        _handler.Response = request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/competitions/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { seasons = new[] { new { seasonId = "refresh-season", seasonName = FantacalcioTime.LabelYear(FantacalcioTime.ActualYear) } } }) };
            if (request.RequestUri.AbsolutePath.Contains("/stats/players"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { players = new[] { new { shortName = _player.Name, role = 4, imagery = new { playerImage_home_middle = "playerImages/api-refresh.webp" } } } }) };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
            return response;
        };
    }

    public async ValueTask DisposeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _users.DeleteAsync(Owner, ct);
        await _players.DeleteAsync(FantacalcioTime.ActualYear, ct);
        if (_snapshot != null) await _players.InsertAsync(FantacalcioTime.ActualYear, _snapshot, ct);
        await _images.DeleteAsync($"players/{_player.Key}.jpg", ct);
    }

    [Fact]
    public async Task ReloadRequiresAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await ReloadAsync(GroupId, _player.Key)).StatusCode);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task ReloadRejectsAnotherGroup()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReloadAsync("another-group", _player.Key)).StatusCode);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task ReloadRejectsUnknownPlayerWithoutContactingSource()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        Assert.Equal(HttpStatusCode.NotFound, (await ReloadAsync(GroupId, "../arbitrary-blob")).StatusCode);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task ParticipantCanRefreshKnownPlayerAndReceivesCacheRevision()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var response = await ReloadAsync(GroupId, _player.Key.ToUpperInvariant());
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(_player.Key, body.GetProperty("playerKey").GetString());
        Assert.True(Guid.TryParseExact(body.GetProperty("revision").GetString(), "N", out _));
        Assert.Equal(new byte[] { 1, 2, 3 }, (await _images.DownloadAsync($"players/{_player.Key}.jpg", cancellationToken: TestContext.Current.CancellationToken))?.Data);
    }

    [Fact]
    public async Task MissingOfficialPhotoPreservesCurrentBlob()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        _handler.Response = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { seasons = Array.Empty<object>() }) };
        Assert.Equal(HttpStatusCode.NotFound, (await ReloadAsync(GroupId, _player.Key)).StatusCode);
        Assert.Equal(new byte[] { 9 }, (await _images.DownloadAsync($"players/{_player.Key}.jpg", cancellationToken: TestContext.Current.CancellationToken))?.Data);
    }

    [Fact]
    public async Task UpstreamFailurePreservesCurrentBlob()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        _handler.Response = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.BadGateway, (await ReloadAsync(GroupId, _player.Key)).StatusCode);
        Assert.Equal(new byte[] { 9 }, (await _images.DownloadAsync($"players/{_player.Key}.jpg", cancellationToken: TestContext.Current.CancellationToken))?.Data);
    }

    private Task<HttpResponseMessage> ReloadAsync(string groupId, string playerKey)
        => _client.PostAsJsonAsync("/PlayerImage/Reload", new { groupId, playerKey, year = FantacalcioTime.ActualYear }, TestContext.Current.CancellationToken);
}
