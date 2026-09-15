using System.Net;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

public sealed class SerieACalendarTest : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ISerieAInformation _serieA;
    private readonly TestSerieACalendarHandler _calendarHandler;

    public SerieACalendarTest()
    {
        var services = new ServiceCollection();
        services.AddSharedBusiness();
        services.AddSingleton<TestSerieACalendarHandler>();
        services.AddHttpClient("seriea-gazzetta-calendar", client => client.BaseAddress = new Uri("https://calendar.test/"))
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestSerieACalendarHandler>());

        _services = services.BuildServiceProvider();
        _serieA = _services.GetRequiredService<ISerieAInformation>();
        _calendarHandler = _services.GetRequiredService<TestSerieACalendarHandler>();
    }

    public void Dispose()
    {
    }

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
    public async Task DoesNotExposePrematchScoreAsResult()
    {
        _calendarHandler.Response = _ => JsonResponse("""
            {"data":{"games":[{"matches":[{"utcDate":"2027-05-30T13:00:00Z","status":"PREMATCH","homeTeam":{"teamName":"Roma","shortTeamName":"rom","score":0},"awayTeam":{"teamName":"Milan","shortTeamName":"mil","score":0}}]}]}}
            """);

        var calendar = await _serieA.GetCalendarAsync(38, 38);

        var game = Assert.Single(Assert.Single(calendar.Days).Games);
        Assert.Null(game.HomeGoal);
        Assert.Null(game.AwayGoal);
    }

    [Fact]
    public async Task ExposesLiveScoreAsResult()
    {
        _calendarHandler.Response = _ => JsonResponse("""
            {"data":{"games":[{"matches":[{"utcDate":"2026-08-27T18:45:00Z","status":"LIVE","homeTeam":{"teamName":"Roma","shortTeamName":"rom","score":2},"awayTeam":{"teamName":"Milan","shortTeamName":"mil","score":1}}]}]}}
            """);

        var calendar = await _serieA.GetCalendarAsync(1, 1);

        var game = Assert.Single(Assert.Single(calendar.Days).Games);
        Assert.Equal(2, game.HomeGoal);
        Assert.Equal(1, game.AwayGoal);
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
                    "status": "FULL",
                    "homeTeam": { "teamName": "Roma", "shortTeamName": "rom", "score": 2 },
                    "awayTeam": { "teamName": "Milan", "shortTeamName": "mil", "score": 1 }
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
