using System.Net;
using Fantasoccer.Engine.IngestionData;
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
    private readonly TestPlayerImagesHandler _handler;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private readonly IContentRepository _images;
    private RealPlayersWrapper? _snapshot;

    public PlayerImagesTest(
        PlayerImagesJob job,
        TestPlayerImagesHandler handler,
        IRepository<RealPlayersWrapper, int> players,
        IFactory<IContentRepository> contentFactory)
    {
        _job = job;
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
