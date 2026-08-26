using System.Net;
using Fantasoccer.Business.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Services;

[Collection("InMemoryRepository")]
public sealed class AllPlayersTest : IAsyncLifetime
{
    private readonly IAllPlayers _allPlayers;
    private readonly TestAllPlayersHandler _handler;
    private readonly IRepository<RealTeamWrapper, int> _teams;
    private RealTeamWrapper? _snapshot;

    public AllPlayersTest(IAllPlayers allPlayers, TestAllPlayersHandler handler, IRepository<RealTeamWrapper, int> teams)
    {
        _allPlayers = allPlayers;
        _handler = handler;
        _teams = teams;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _snapshot = await _teams.GetAsync(year);
        await _teams.DeleteAsync(year);
        await _teams.InsertAsync(year, new RealTeamWrapper
        {
            Teams = [new RealTeam { Name = "Roma" }, new RealTeam { Name = "Milan" }]
        });
        _handler.Responses = 0;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        await _teams.DeleteAsync(year);
        if (_snapshot != null)
            await _teams.InsertAsync(year, _snapshot);
    }

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
