using System.Net;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

public sealed class AllPlayersTest : IDisposable
{
    private readonly IAllPlayers _allPlayers;
    private readonly TestAllPlayersHandler _handler;
    private readonly ServiceProvider _services;

    public AllPlayersTest()
    {
        var services = new ServiceCollection();
        services.AddFantasoccerSqlDataProviders();
        services.AddSingleton<IFootballIngestionReader, TestIngestionReader>();
        services.AddSingleton<TestAllPlayersHandler>();
        services.AddHttpClient("all-players").ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestAllPlayersHandler>());
        _services = services.BuildServiceProvider();
        _allPlayers = _services.GetRequiredService<IAllPlayers>();
        _handler = _services.GetRequiredService<TestAllPlayersHandler>();
    }

    private sealed class TestIngestionReader : IFootballIngestionReader
    {
        public Task<RealCalendar?> CalendarAsync(int year) => Task.FromResult<RealCalendar?>(null);
        public Task<RealPlayersWrapper?> PlayersAsync(int year) => Task.FromResult<RealPlayersWrapper?>(null);
        public Task<RealTeamWrapper?> ClubsAsync(int year) => Task.FromResult<RealTeamWrapper?>(new() { Teams = [new() { Name = "Roma" }, new() { Name = "Milan" }] });
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task ParsesPlayersGroupsByTeamAndSkipsOutOfGamePlayers()
    {
        var players = await _allPlayers.GetAsync();

        Assert.Equal(2, players.Count);
        Assert.Equal(2, players.GroupBy(x => x.RealTeam.Name).Count());
        Assert.Equal(Role.Forward, players.Single(x => x.Name == "Mario Rossi").Role);
        Assert.Equal(Role.GoalKeeper, players.Single(x => x.Name == "Luca Bianchi").Role);
        Assert.DoesNotContain(players, x => x.Name == "Inactive Player");
        Assert.Equal(1, _handler.Responses);
    }
}

public sealed class TestAllPlayersHandler : HttpMessageHandler
{
    public int Responses { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Responses++;
        var content = """
            <tr class="player-row"><span>Mario Rossi</span><td class="player-team">rom</td><span class="role" data-value="a"></span></tr>
            <tr class="player-row"><span>Luca Bianchi</span><td class="player-team">mil</td><span class="role" data-value="p"></span></tr>
            <tr class="player-row out-of-game"><span>Inactive Player</span><td class="player-team">rom</td><span class="role" data-value="d"></span></tr>
            """;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
    }
}
