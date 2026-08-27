using System.Net;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

public sealed class SerieACalendarTest : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ISerieAInformation _serieA;
    private readonly TestSerieACalendarHandler _calendarHandler;
    private readonly TestSerieARankHandler _rankHandler;

    public SerieACalendarTest()
    {
        var services = new ServiceCollection();
        services.AddSharedBusiness();
        services.AddSingleton<TestSerieACalendarHandler>();
        services.AddSingleton<TestSerieARankHandler>();
        services.AddHttpClient("seriea-gazzetta-calendar", client => client.BaseAddress = new Uri("https://calendar.test/"))
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestSerieACalendarHandler>());
        services.AddHttpClient("seriea")
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestSerieARankHandler>());

        _services = services.BuildServiceProvider();
        _serieA = _services.GetRequiredService<ISerieAInformation>();
        _calendarHandler = _services.GetRequiredService<TestSerieACalendarHandler>();
        _rankHandler = _services.GetRequiredService<TestSerieARankHandler>();
    }

    public void Dispose()
        => _services.Dispose();

    [Fact]
    public async Task KeepsValidDaysWhenAnotherDayIsNotFound()
    {
        _calendarHandler.Response = request => request.RequestUri?.Query.Contains("day=1") == true
            ? JsonResponse(ValidCalendarJson)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        var calendar = await _serieA.GetCalendarAsync(1, 2);

        Assert.Equal(FantacalcioTime.ActualYear, calendar.Year);
        var day = Assert.Single(calendar.Days);
        Assert.Equal(1, day.SerieADay);
        var game = Assert.Single(day.Games);
        Assert.Equal("Roma", game.Home.Name);
        Assert.Equal("Milan", game.Away.Name);
        Assert.Equal(2, game.HomeGoal);
        Assert.Equal(1, game.AwayGoal);
    }

    [Fact]
    public async Task ReturnsEmptyCalendarForInvalidJson()
    {
        _calendarHandler.Response = _ => JsonResponse("not-json");

        var calendar = await _serieA.GetCalendarAsync(1, 1);

        Assert.Empty(calendar.Days);
    }

    [Fact]
    public async Task SkipsMatchesWithIncompleteTeams()
    {
        _calendarHandler.Response = _ => JsonResponse("""
            {"data":{"games":[{"matches":[{"utcDate":"2026-08-30T18:45:00Z","homeTeam":{"teamName":"","shortTeamName":"rom","score":2},"awayTeam":{"teamName":"Milan","shortTeamName":"mil","score":1}}]}]}}
            """);

        var calendar = await _serieA.GetCalendarAsync(1, 1);

        Assert.Empty(Assert.Single(calendar.Days).Games);
    }

    [Fact]
    public async Task ReturnsEmptyRankWhenEndpointIsNotFound()
    {
        _rankHandler.Response = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var rank = await _serieA.GetRankAsync();

        Assert.Empty(rank.Teams);
    }

    [Fact]
    public async Task ReturnsEmptyRankForInvalidJson()
    {
        _rankHandler.Response = _ => JsonResponse("not-json");

        var rank = await _serieA.GetRankAsync();

        Assert.Empty(rank.Teams);
    }

    [Fact]
    public async Task ParsesAndOrdersRank()
    {
        _rankHandler.Response = _ => JsonResponse(ValidRankJson);

        var rank = await _serieA.GetRankAsync();

        Assert.Collection(rank.Teams,
            first =>
            {
                Assert.Equal("Inter", first.Name);
                Assert.Equal(6, first.Point);
            },
            second =>
            {
                Assert.Equal("Roma", second.Name);
                Assert.Equal(3, second.Point);
                Assert.Equal(2, second.Goal);
            });
    }

    private static HttpResponseMessage JsonResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };

    private const string ValidCalendarJson = """
        {
          "data": {
            "games": [
              {
                "matches": [
                  {
                    "utcDate": "2026-08-30T18:45:00Z",
                    "homeTeam": { "teamName": "Roma", "shortTeamName": "rom", "score": 2 },
                    "awayTeam": { "teamName": "Milan", "shortTeamName": "mil", "score": 1 }
                  }
                ]
              }
            ]
          }
        }
        """;

    private const string ValidRankJson = """
        {
          "data": {
            "standings": [
              {
                "teams": [
                  {
                    "officialName": "Roma",
                    "stats": [
                      { "statsId": "points", "statsValue": 3 },
                      { "statsId": "goals-for", "statsValue": 2 }
                    ]
                  },
                  {
                    "officialName": "Inter",
                    "stats": [
                      { "statsId": "points", "statsValue": 6 }
                    ]
                  }
                ]
              }
            ]
          }
        }
        """;
}

public sealed class TestSerieACalendarHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; }
        = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Response(request));
}

public sealed class TestSerieARankHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; }
        = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Response(request));
}
