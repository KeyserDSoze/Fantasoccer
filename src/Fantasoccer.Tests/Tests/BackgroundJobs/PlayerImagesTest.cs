using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Engine.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

public sealed class PlayerImagesTest : IAsyncLifetime
{
    private readonly PlayerImagesJob _job;
    private readonly TestPlayerImagesHandler _handler;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private RealPlayersWrapper? _snapshot;

    public PlayerImagesTest(
        PlayerImagesJob job,
        TestPlayerImagesHandler handler,
        IRepository<RealPlayersWrapper, int> players)
    {
        _job = job;
        _handler = handler;
        _players = players;
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _snapshot = await _players.GetAsync(year);
        await _players.DeleteAsync(year);
        _handler.StatusCode = HttpStatusCode.NotFound;
        _handler.Requests = 0;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        await _players.DeleteAsync(year);
        if (_snapshot != null)
            await _players.InsertAsync(year, _snapshot);
    }

    [Fact]
    public async Task IgnoresUnavailableImageCatalog()
    {
        await _players.InsertAsync(FantacalcioTime.ActualYear, new RealPlayersWrapper
        {
            Players =
            [
                new RealPlayer
                {
                    Name = "Mario Rossi",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    IsActive = true
                }
            ]
        });

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _handler.Requests);
    }
}

public sealed class TestPlayerImagesHandler : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NotFound;
    public int Requests { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(StatusCode));
    }
}
