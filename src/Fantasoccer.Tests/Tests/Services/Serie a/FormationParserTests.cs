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

    [Theory]
    [InlineData("Fantagazzetta")]
    [InlineData("Gazzetta")]
    [InlineData("Injury")]
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
    public async Task PropagatesProviderHttpErrors(string parserName)
    {
        _handler.StatusCode = HttpStatusCode.NotFound;

        await Assert.ThrowsAsync<HttpRequestException>(() => Parser(parserName).GetPlayersAsync());
    }

    private IFormationParser Parser(string name)
        => _factory.Create(name) ?? throw new InvalidOperationException($"Parser {name} is not registered.");
}

public sealed class TestFormationHttpHandler : HttpMessageHandler
{
    public string Content { get; set; } = string.Empty;
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public Uri? LastRequestUri { get; private set; }
    public string UserAgent { get; private set; } = string.Empty;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        UserAgent = request.Headers.UserAgent.ToString();
        return Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(Content)
        });
    }
}
