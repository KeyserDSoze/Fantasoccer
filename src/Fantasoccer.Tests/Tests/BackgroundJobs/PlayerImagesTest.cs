using System.Net;
using Fantasoccer.Engine.IngestionData;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class PlayerImagesTest : IAsyncLifetime
{
    private const string PreviousPlayerImage = "players/previousimageplayer.jpg";
    private const string CurrentPlayerImage = "players/currentimageplayer.jpg";
    private readonly PlayerImagesJob _job;
    private readonly PlayerImageService _service;
    private readonly TestPlayerImagesHandler _handler;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private readonly IContentRepository _images;
    private RealPlayersWrapper? _snapshot;

    public PlayerImagesTest(
        PlayerImagesJob job,
        PlayerImageService service,
        TestPlayerImagesHandler handler,
        IRepository<RealPlayersWrapper, int> players,
        IFactory<IContentRepository> contentFactory)
    {
        _job = job;
        _service = service;
        _handler = handler;
        _players = players;
        _images = contentFactory.Create("images")!;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        _snapshot = await _players.GetAsync(year, cancellationToken);
        await _players.DeleteAsync(year, cancellationToken);
        await _images.DeleteAsync(PreviousPlayerImage, cancellationToken);
        await _images.DeleteAsync(CurrentPlayerImage, cancellationToken);
        _handler.Response = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        _handler.Requests.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        var cancellationToken = TestContext.Current.CancellationToken;
        await _players.DeleteAsync(year, cancellationToken);
        if (_snapshot != null)
            await _players.InsertAsync(year, _snapshot, cancellationToken);
        await _images.DeleteAsync(PreviousPlayerImage, cancellationToken);
        await _images.DeleteAsync(CurrentPlayerImage, cancellationToken);
    }

    [Fact]
    public async Task IgnoresUnavailableSeasonCatalog()
    {
        await InsertPlayersAsync(CreatePlayer("Previous Image Player", "Roma", Role.Forward));

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(_handler.Requests);
        Assert.Contains("/competitions/", request.AbsolutePath);
        Assert.False(await _images.ExistAsync(PreviousPlayerImage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadsCurrentImagesAndFallsBackToPreviousSeason()
    {
        await InsertPlayersAsync(
            CreatePlayer("Previous Image Player", "Roma", Role.Forward),
            CreatePlayer("Current Image Player", "Milan", Role.Defensor));
        _handler.Response = Response;

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var previous = await _images.DownloadAsync(PreviousPlayerImage, cancellationToken: TestContext.Current.CancellationToken);
        var current = await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 1, 2, 3 }, previous?.Data);
        Assert.Equal(new byte[] { 4, 5, 6 }, current?.Data);
        Assert.Equal(2, _handler.Requests.Count(x => x.Host == "media-sdp.legaseriea.it"));
        Assert.Contains(_handler.Requests, x => x.Query.Contains("page=2"));
        Assert.All(_handler.UserAgents, x => Assert.Contains("Mozilla", x));
    }

    [Fact]
    public async Task DoesNotDownloadAnExistingImage()
    {
        await InsertPlayersAsync(CreatePlayer("Current Image Player", "Milan", Role.Defensor));
        await _images.UploadAsync(CurrentPlayerImage, [9], new ContentRepositoryOptions
        {
            HttpHeaders = new ContentRepositoryHttpHeaders { ContentType = "image/webp" }
        }, true, TestContext.Current.CancellationToken);
        _handler.Response = Response;

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var image = await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 9 }, image?.Data);
        Assert.DoesNotContain(_handler.Requests, x => x.Host == "media-sdp.legaseriea.it");
    }

    [Fact]
    public async Task ContinuesWhenOneImageDownloadFails()
    {
        await InsertPlayersAsync(
            CreatePlayer("Previous Image Player", "Roma", Role.Forward),
            CreatePlayer("Current Image Player", "Milan", Role.Defensor));
        _handler.Response = request => request.RequestUri!.AbsolutePath.EndsWith("previous-player.webp")
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Response(request);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.False(await _images.ExistAsync(PreviousPlayerImage, TestContext.Current.CancellationToken));
        var current = await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 4, 5, 6 }, current?.Data);
    }

    [Fact]
    public async Task ManualReloadOverwritesExistingBlobAndRereadsSource()
    {
        var player = CreatePlayer("Current Image Player", "Milan", Role.Defensor);
        await _images.UploadAsync(CurrentPlayerImage, [9], new ContentRepositoryOptions
        {
            HttpHeaders = new ContentRepositoryHttpHeaders { ContentType = "image/webp" }
        }, true, TestContext.Current.CancellationToken);
        _handler.Response = Response;

        Assert.True(await _service.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 4, 5, 6 }, (await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken))?.Data);
        _handler.Response = request => request.RequestUri!.AbsolutePath.EndsWith("current-player.webp") ? ImageResponse([7, 8]) : Response(request);
        Assert.True(await _service.ReloadAsync(player, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 7, 8 }, (await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken))?.Data);
        Assert.Equal(2, _handler.Requests.Count(x => x.Host == "media-sdp.legaseriea.it"));
    }

    [Fact]
    public async Task ManualReloadUsesPreviousSeasonFallback()
    {
        _handler.Response = Response;
        Assert.True(await _service.ReloadAsync(CreatePlayer("Previous Image Player", "Roma", Role.Forward), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 1, 2, 3 }, (await _images.DownloadAsync(PreviousPlayerImage, cancellationToken: TestContext.Current.CancellationToken))?.Data);
    }

    [Fact]
    public async Task ManualReloadMatchesPlayerWhenOfficialNameHasAnAdditionalGivenName()
    {
        var player = CreatePlayer("Floriani Mussolini", "Lazio", Role.Defensor);
        _handler.Response = request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/competitions/")) return JsonResponse(SeasonsJson);
            if (request.RequestUri.AbsolutePath.Contains("current-season"))
                return JsonResponse("""
                    {
                      "pagination": { "totalPages": 1, "currentPage": 1, "isLastPage": true },
                      "players": [{
                        "mediaFirstName": "Romano",
                        "mediaLastName": "Floriani Mussolini",
                        "shortName": "Floriani M.",
                        "displayName": "Floriani M.",
                        "role": 2,
                        "team": { "shortName": "Lazio", "officialName": "Lazio", "acronymName": "LAZ" },
                        "imagery": { "playerImage_home_middle": "playerImages/floriani-mussolini.webp" }
                      }]
                    }
                    """);
            if (request.RequestUri.AbsolutePath.Contains("previous-season")) return JsonResponse(PreviousPlayersJson);
            if (request.RequestUri.AbsolutePath.EndsWith("floriani-mussolini.webp")) return ImageResponse([1, 2, 3]);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        try
        {
            Assert.True(await _service.ReloadAsync(player, TestContext.Current.CancellationToken));
            Assert.Contains(_handler.Requests, x => x.AbsolutePath.EndsWith("floriani-mussolini.webp"));
        }
        finally
        {
            await _images.DeleteAsync($"players/{player.Key}.jpg", TestContext.Current.CancellationToken);
        }
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
    public async Task ManualReloadUsesAdjacentRolesButKeepsGoalkeepersSeparate(Role playerRole, int sourceRole, bool expected)
    {
        var player = CreatePlayer("Role Match Player", "Team", playerRole);
        _handler.Response = request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/competitions/")) return JsonResponse(SeasonsJson);
            if (request.RequestUri.AbsolutePath.Contains("current-season"))
                return JsonResponse($$"""
                    {
                      "pagination": { "totalPages": 1, "currentPage": 1, "isLastPage": true },
                      "players": [{
                        "shortName": "Role Match Player",
                        "displayName": "Role Match Player",
                        "role": {{sourceRole}},
                        "team": { "shortName": "Team", "officialName": "Team", "acronymName": "T" },
                        "imagery": { "playerImage_home_middle": "playerImages/role-match.webp" }
                      }]
                    }
                    """);
            if (request.RequestUri.AbsolutePath.Contains("previous-season")) return JsonResponse(PreviousPlayersJson);
            if (request.RequestUri.AbsolutePath.EndsWith("role-match.webp")) return ImageResponse([1, 2, 3]);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        try
        {
            Assert.Equal(expected, await _service.ReloadAsync(player, TestContext.Current.CancellationToken));
        }
        finally
        {
            await _images.DeleteAsync($"players/{player.Key}.jpg", TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ManualReloadDoesNotReplaceImageForWrongRoleOrUnknownPlayer()
    {
        _handler.Response = Response;
        await _images.UploadAsync(CurrentPlayerImage, [9], overwrite: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await _service.ReloadAsync(CreatePlayer("Current Image Player", "Milan", Role.Forward), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 9 }, (await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken))?.Data);
        Assert.DoesNotContain(_handler.Requests, x => x.Host == "media-sdp.legaseriea.it");
    }

    [Fact]
    public async Task ManualReloadRejectsImageUrlsOutsideOfficialHost()
    {
        _handler.Response = request => request.RequestUri!.Query.Contains("page=2")
            ? JsonResponse(CurrentPlayersPageTwoJson.Replace("playerImages/current-player.webp", "http://127.0.0.1/private"))
            : Response(request);

        await Assert.ThrowsAsync<InvalidDataException>(() => _service.ReloadAsync(CreatePlayer("Current Image Player", "Milan", Role.Defensor), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(_handler.Requests, x => x.Host == "127.0.0.1");
    }

    [Theory]
    [InlineData("text/html", false)]
    [InlineData("image/svg+xml", false)]
    [InlineData("image/webp", true)]
    public async Task ManualReloadPreservesExistingImageWhenSourceIsInvalid(string contentType, bool empty)
    {
        await _images.UploadAsync(CurrentPlayerImage, [9], overwrite: true, cancellationToken: TestContext.Current.CancellationToken);
        _handler.Response = request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("current-player.webp")) return Response(request);
            var response = ImageResponse(empty ? [] : [1]);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return response;
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => _service.ReloadAsync(CreatePlayer("Current Image Player", "Milan", Role.Defensor), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 9 }, (await _images.DownloadAsync(CurrentPlayerImage, cancellationToken: TestContext.Current.CancellationToken))?.Data);
    }

    private HttpResponseMessage Response(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        if (uri.AbsolutePath.Contains("/competitions/"))
            return JsonResponse(SeasonsJson);
        if (uri.AbsolutePath.Contains("current-season") && uri.Query.Contains("page=1"))
            return JsonResponse(CurrentPlayersPageOneJson);
        if (uri.AbsolutePath.Contains("current-season") && uri.Query.Contains("page=2"))
            return JsonResponse(CurrentPlayersPageTwoJson);
        if (uri.AbsolutePath.Contains("previous-season"))
            return JsonResponse(PreviousPlayersJson);
        if (uri.AbsolutePath.EndsWith("previous-player.webp"))
            return ImageResponse([1, 2, 3]);
        if (uri.AbsolutePath.EndsWith("current-player.webp"))
            return ImageResponse([4, 5, 6]);
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private async Task InsertPlayersAsync(params RealPlayer[] players)
    {
        await _players.InsertAsync(FantacalcioTime.ActualYear, new RealPlayersWrapper
        {
            Players = [.. players]
        }, TestContext.Current.CancellationToken);
    }

    private static RealPlayer CreatePlayer(string name, string team, Role role)
        => new()
        {
            Name = name,
            RealTeam = new RealTeam { Name = team },
            Role = role,
            IsActive = true
        };

    private static HttpResponseMessage JsonResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ImageResponse(byte[] content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
        return response;
    }

    private static string SeasonsJson => $$"""
        {
          "seasons": [
            { "seasonId": "current-season", "seasonName": "{{FantacalcioTime.LabelYear(FantacalcioTime.ActualYear)}}" },
            { "seasonId": "previous-season", "seasonName": "{{FantacalcioTime.LabelYear(FantacalcioTime.ActualYear - 1)}}" }
          ]
        }
        """;

    private const string CurrentPlayersPageOneJson = """
        {
          "pagination": { "totalPages": 2, "currentPage": 1, "isLastPage": false },
          "players": [
            {
              "shortName": "Previous Image Player",
              "displayName": "Previous Image Player",
              "role": 4,
              "team": { "shortName": "Roma", "officialName": "Roma", "acronymName": "ROM" },
              "imagery": {}
            }
          ]
        }
        """;

    private const string CurrentPlayersPageTwoJson = """
        {
          "pagination": { "totalPages": 2, "currentPage": 2, "isLastPage": true },
          "players": [
            {
              "shortName": "Current Image Player",
              "displayName": "Current Image Player",
              "role": 2,
              "team": { "shortName": "Milan", "officialName": "Milan", "acronymName": "MIL" },
              "imagery": { "playerImage_home_middle": "playerImages/current-player.webp" }
            }
          ]
        }
        """;

    private const string PreviousPlayersJson = """
        {
          "pagination": { "totalPages": 1, "currentPage": 1, "isLastPage": true },
          "players": [
            {
              "shortName": "Previous Image Player",
              "displayName": "Previous Image Player",
              "role": 4,
              "team": { "shortName": "Roma", "officialName": "Roma", "acronymName": "ROM" },
              "imagery": { "playerImage_home_middle": "playerImages/previous-player.webp" }
            }
          ]
        }
        """;
}

public sealed class TestPlayerImagesHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; }
        = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
    public List<Uri> Requests { get; } = [];
    public List<string> UserAgents { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        UserAgents.Add(request.Headers.UserAgent.ToString());
        return Task.FromResult(Response(request));
    }
}
