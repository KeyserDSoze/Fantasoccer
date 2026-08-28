using System.Net;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

public sealed class FormationParserTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IFactory<IFormationParser> _factory;
    private readonly TestFormationHttpHandler _handler;

    public FormationParserTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SocialLogin:Microsoft:ClientId"] = "test-client-id",
                ["SocialLogin:Microsoft:ClientSecret"] = "test-client-secret",
                ["SocialLogin:Microsoft:RedirectDomains"] = "https://localhost"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddFantasoccerBusiness(configuration);
        services.AddSingleton<TestFormationHttpHandler>();
        services.AddHttpClient("base")
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestFormationHttpHandler>());

        _services = services.BuildServiceProvider();
        _factory = _services.GetRequiredService<IFactory<IFormationParser>>();
        _handler = _services.GetRequiredService<TestFormationHttpHandler>();
    }

    public void Dispose()
        => _services.Dispose();

    [Fact]
    public async Task FantagazzettaParsesStartingPlayers()
    {
        _handler.Content = """
            <ul class="player-list starters">
                <li class="player-item"><a href="https://www.fantacalcio.it/serie-a/squadre/roma/rosa"><span>Mario Rossi</span></a></li>
            </ul>
            <ul class="player-list starters">
                <li class="player-item"><a href="https://www.fantacalcio.it/serie-a/squadre/milan/rosa"><span>Luca Bianchi</span></a></li>
            </ul>
            """;

        var players = await Parser("Fantagazzetta").GetPlayersAsync();

        Assert.Collection(players,
            player =>
            {
                Assert.Equal("Mario Rossi", player.Name);
                Assert.Equal("Roma", player.RealTeam.Name);
                Assert.True(player.Chance.Fantagazzetta);
            },
            player =>
            {
                Assert.Equal("Luca Bianchi", player.Name);
                Assert.Equal("Milan", player.RealTeam.Name);
            });
        Assert.Equal("www.fantacalcio.it", _handler.LastRequestUri?.Host);
        Assert.Contains("Mozilla", _handler.UserAgent);
    }

    [Fact]
    public async Task FantagazzettaSkipsPlayerWithoutTeamLink()
    {
        _handler.Content = """
            <ul class="player-list starters">
                <li class="player-item"><span>Malformed Player</span></li>
            </ul>
            """;

        var players = await Parser("Fantagazzetta").GetPlayersAsync();

        Assert.Empty(players);
    }

    [Fact]
    public async Task GazzettaParsesBothLineups()
    {
        _handler.Content = """
            <div class="bck-box-match-details">
                <a href="https://www.gazzetta.it/calcio/squadre/roma/">Roma</a>
                <a href="https://www.gazzetta.it/calcio/squadre/milan/">Milan</a>
                <ul><li><span class="lineup-team__name">Mario Rossi</span></li></ul>
                <ul><li><span class="lineup-team__name">Luca Bianchi</span></li></ul>
                <div class="go-above-container"></div>
            </div>
            """;

        var players = await Parser("Gazzetta").GetPlayersAsync();

        Assert.Collection(players,
            player =>
            {
                Assert.Equal("mario rossi", player.Name);
                Assert.Equal("Roma", player.RealTeam.Name);
                Assert.True(player.Chance.Gazzetta);
            },
            player =>
            {
                Assert.Equal("luca bianchi", player.Name);
                Assert.Equal("Milan", player.RealTeam.Name);
            });
        Assert.Equal("www.gazzetta.it", _handler.LastRequestUri?.Host);
    }

    [Fact]
    public async Task GazzettaSkipsIncompleteMatchMarkup()
    {
        _handler.Content = "<div class=\"bck-box-match-details\"><ul></ul></div>";

        var players = await Parser("Gazzetta").GetPlayersAsync();

        Assert.Empty(players);
    }

    [Fact]
    public async Task InjuryParsesStatusesAndDescriptions()
    {
        _handler.Content = """
            <div id="team-roma">
                <span class="team-name">Roma</span>
                <header><strong class="item-name">Paulo Dybala</strong><p>Lesione muscolare</p></header>
                <header><strong class="item-name">Lorenzo Pellegrini</strong><p class="item-description">In dubbio</p></header>
            </div>
            """;

        var players = await Parser("Injury").GetPlayersAsync();

        Assert.Collection(players,
            player =>
            {
                Assert.Equal("paulo dybala", player.Name);
                Assert.Equal("Roma", player.RealTeam.Name);
                Assert.Equal(ChanceType.Injury, player.Chance.Status);
                Assert.Equal("Lesione muscolare", player.Chance.Description);
            },
            player =>
            {
                Assert.Equal("lorenzo pellegrini", player.Name);
                Assert.Equal(ChanceType.Warned, player.Chance.Status);
                Assert.Equal("In dubbio", player.Chance.Description);
            });
        Assert.Equal("www.fantacalcio.it", _handler.LastRequestUri?.Host);
    }

    [Fact]
    public async Task SkyParsesEmbeddedWidgetData()
    {
        _handler.Content = """
            <ld-football-competition-predicted-lineups>
              <script type="application/json" data-props="true">
                {
                  "matchList": [
                    {
                      "home": {
                        "name": "Inter",
                        "playerList": {
                          "startingLineup": [
                            { "name": "Lautaro", "surname": "Martínez", "fullName": "Lautaro Martínez" }
                          ]
                        }
                      },
                      "away": {
                        "name": "Milan",
                        "playerList": {
                          "startingLineup": [
                            { "name": "Luka", "surname": "Modric", "fullName": "Modric L." }
                          ]
                        }
                      }
                    }
                  ]
                }
              </script>
            </ld-football-competition-predicted-lineups>
            """;

        var players = await Parser("Sky").GetPlayersAsync();

        Assert.Collection(players,
            player =>
            {
                Assert.Equal("Lautaro Martínez", player.Name);
                Assert.Equal("Inter", player.RealTeam.Name);
                Assert.True(player.Chance.Sky);
            },
            player =>
            {
                Assert.Equal("Luka Modric", player.Name);
                Assert.Equal("Milan", player.RealTeam.Name);
            });
        Assert.Equal("https://sport.sky.it/football/predicted-lineup-homepage/21/widget.html", _handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task SkyReturnsEmptyForMalformedEmbeddedJson()
    {
        _handler.Content = """
            <script type="application/json" data-props="true">not-json</script>
            """;

        var players = await Parser("Sky").GetPlayersAsync();

        Assert.Empty(players);
    }

    [Fact]
    public async Task MediasetDiscoversMatchesAndParsesStartingPlayers()
    {
        _handler.Response = request => request.RequestUri?.Query switch
        {
            "" => HtmlResponse("""
                <a data-testid="cart-match-item" href="?match=roma%3Amilan">Roma-Milan</a>
                <a data-testid="cart-match-item" href="?match=inter%3Anapoli">Inter-Napoli</a>
                <a data-testid="cart-match-item" href="?match=roma%3Amilan">Duplicate</a>
                """),
            "?match=roma%3Amilan" => HtmlResponse(MediasetMatch("Roma", "Milan", "Paulo Dybala", "Mike Maignan")),
            "?match=inter%3Anapoli" => HtmlResponse(MediasetMatch("Inter", "Napoli", "Lautaro Martinez", "Romelu Lukaku")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var players = await Parser("Mediaset").GetPlayersAsync();

        Assert.Equal(4, players.Count);
        Assert.Contains(players, x => x.Name == "Paulo Dybala" && x.RealTeam.Name == "Roma" && x.Chance.Mediaset);
        Assert.Contains(players, x => x.Name == "Mike Maignan" && x.RealTeam.Name == "Milan" && x.Chance.Mediaset);
        Assert.Contains(players, x => x.Name == "Lautaro Martinez" && x.RealTeam.Name == "Inter" && x.Chance.Mediaset);
        Assert.Contains(players, x => x.Name == "Romelu Lukaku" && x.RealTeam.Name == "Napoli" && x.Chance.Mediaset);
        Assert.Equal(3, _handler.Requests.Count);
        Assert.Equal("https://www.sportmediaset.mediaset.it/probabili-formazioni/serie-a/", _handler.Requests[0].ToString());
    }

    [Fact]
    public async Task MediasetContinuesWhenOneMatchIsUnavailable()
    {
        _handler.Response = request => request.RequestUri?.Query switch
        {
            "" => HtmlResponse("""
                <a data-testid="cart-match-item" href="?match=roma%3Amilan">Roma-Milan</a>
                <a data-testid="cart-match-item" href="?match=missing">Missing</a>
                """),
            "?match=roma%3Amilan" => HtmlResponse(MediasetMatch("Roma", "Milan", "Paulo Dybala", "Mike Maignan")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var players = await Parser("Mediaset").GetPlayersAsync();

        Assert.Equal(2, players.Count);
    }

    [Fact]
    public async Task MediasetSkipsIncompleteMatchMarkup()
    {
        _handler.Response = request => string.IsNullOrEmpty(request.RequestUri?.Query)
            ? HtmlResponse("<a data-testid=\"cart-match-item\" href=\"?match=broken\">Broken</a>")
            : HtmlResponse("<div data-testid=\"team-eleven\"></div>");

        var players = await Parser("Mediaset").GetPlayersAsync();

        Assert.Empty(players);
    }

    [Theory]
    [InlineData("Fantagazzetta")]
    [InlineData("Gazzetta")]
    [InlineData("Injury")]
    [InlineData("Sky")]
    [InlineData("Mediaset")]
    public async Task ReturnsEmptyForEmptyProviderResponse(string parserName)
    {
        _handler.Content = string.Empty;

        var players = await Parser(parserName).GetPlayersAsync();

        Assert.Empty(players);
    }

    [Theory]
    [InlineData("Fantagazzetta")]
    [InlineData("Gazzetta")]
    [InlineData("Injury")]
    [InlineData("Sky")]
    [InlineData("Mediaset")]
    public async Task PropagatesProviderHttpErrors(string parserName)
    {
        _handler.StatusCode = HttpStatusCode.NotFound;

        await Assert.ThrowsAsync<HttpRequestException>(() => Parser(parserName).GetPlayersAsync());
    }

    private IFormationParser Parser(string name)
        => _factory.Create(name) ?? throw new InvalidOperationException($"Parser {name} is not registered.");

    private static string MediasetMatch(string homeTeam, string awayTeam, string homePlayer, string awayPlayer)
        => $$"""
            <div data-testid="match-heading-container">
                <span data-testid="team-info-title">{{homeTeam}}</span>
                <span data-testid="team-info-title">{{homeTeam}}</span>
                <span data-testid="team-info-title">{{awayTeam}}</span>
                <span data-testid="team-info-title">{{awayTeam}}</span>
            </div>
            <div data-testid="team-eleven">
                <span data-testid="player-name">{{homePlayer}}</span>
            </div>
            <div data-testid="team-eleven">
                <span data-testid="player-name">{{awayPlayer}}</span>
            </div>
            """;

    private static HttpResponseMessage HtmlResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
}

public sealed class TestFormationHttpHandler : HttpMessageHandler
{
    public string Content { get; set; } = string.Empty;
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public Func<HttpRequestMessage, HttpResponseMessage>? Response { get; set; }
    public List<Uri> Requests { get; } = [];
    public Uri? LastRequestUri { get; private set; }
    public string UserAgent { get; private set; } = string.Empty;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        Requests.Add(request.RequestUri!);
        UserAgent = request.Headers.UserAgent.ToString();
        if (Response != null)
            return Task.FromResult(Response(request));
        return Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(Content)
        });
    }
}
