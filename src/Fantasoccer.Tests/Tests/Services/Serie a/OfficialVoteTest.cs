using System.Net;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Services;

[Collection("InMemoryRepository")]
public sealed class OfficialVoteTest : IAsyncLifetime
{
    private readonly ServiceProvider _services;
    private readonly IOfficialVote _officialVote;
    private readonly TestOfficialVoteHandler _handler;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<RealPlayersWrapper, int> _players;
    private RealCalendar? _calendarSnapshot;
    private RealPlayersWrapper? _playersSnapshot;

    public OfficialVoteTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddSharedBusiness();
        services.AddSingleton<TestOfficialVoteHandler>();
        services.AddHttpClient("base")
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestOfficialVoteHandler>());

        _services = services.BuildServiceProvider();
        _officialVote = _services.GetRequiredService<IOfficialVote>();
        _handler = _services.GetRequiredService<TestOfficialVoteHandler>();
        _calendars = _services.GetRequiredService<IRepository<RealCalendar, int>>();
        _players = _services.GetRequiredService<IRepository<RealPlayersWrapper, int>>();
    }

    public async ValueTask InitializeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        _calendarSnapshot = await _calendars.GetAsync(year);
        _playersSnapshot = await _players.GetAsync(year);
        await _calendars.DeleteAsync(year);
        await _players.DeleteAsync(year);
        _handler.Content = string.Empty;
        _handler.StatusCode = HttpStatusCode.OK;
    }

    public async ValueTask DisposeAsync()
    {
        var year = FantacalcioTime.ActualYear;
        await _calendars.DeleteAsync(year);
        await _players.DeleteAsync(year);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(year, _calendarSnapshot);
        if (_playersSnapshot != null)
            await _players.InsertAsync(year, _playersSnapshot);

    }

    [Fact]
    public async Task ParsesOfficialVoteHtml()
    {
        _handler.Content = ValidOfficialVotesHtml;

        var players = await _officialVote.GetAsync(FantacalcioTime.ActualYear, 1);

        var player = Assert.Single(players);
        Assert.Equal("Mario Rossi", player.Name);
        Assert.Equal("Roma", player.RealTeam.Name);
        Assert.Equal(Role.Forward, player.Role);
        Assert.True(player.Vote.IsFinal);
        Assert.True(player.Vote.HasVote);
        Assert.Equal(6.5F, player.Vote.Value);
        Assert.Equal(1, player.Vote.Goal);
    }

    [Fact]
    public async Task ReturnsEmptyWhenTeamsBlockIsMissing()
    {
        _handler.Content = "<html><body>missing expected markup</body></html>";

        var players = await _officialVote.GetAsync(FantacalcioTime.ActualYear, 1);

        Assert.Empty(players);
    }

    [Fact]
    public async Task SkipsIncompletePlayerRows()
    {
        _handler.Content = """
            <ul class="teams">
                <a class="team-name" content="roma"></a>
                <tbody>
                    <div class="player-item"><span>Mario Rossi</span><span data-value="a"></span><td></td></div>
                </tbody>
            </ul>
            """;

        var players = await _officialVote.GetAsync(FantacalcioTime.ActualYear, 1);

        Assert.Empty(players);
    }

    [Fact]
    public async Task ParsesPlayerWhenBonusValuesAreMissing()
    {
        _handler.Content = """
            <ul class="teams">
                <a class="team-name" content="roma"></a>
                <tbody>
                    <div class="player-item">
                        <span>Mario Rossi</span>
                        <span class="role" data-value="a"></span>
                        <td></td>
                        <td><span data-value="6,5">6,5</span></td>
                    </div>
                </tbody>
            </ul>
            """;

        var players = await _officialVote.GetAsync(FantacalcioTime.ActualYear, 1);

        var player = Assert.Single(players);
        Assert.Equal("Mario Rossi", player.Name);
        Assert.Equal(0, player.Vote.Goal);
        Assert.Equal(0, player.Vote.Assist);
        Assert.False(player.Vote.ManOfTheMatch);
    }

    [Fact]
    public async Task DoesNotFailWhenCalendarDayIsMissing()
    {
        var year = FantacalcioTime.ActualYear;
        _handler.Content = ValidOfficialVotesHtml;
        await _calendars.InsertAsync(year, new RealCalendar
        {
            Year = year,
            Days = []
        }, TestContext.Current.CancellationToken);

        var players = await _officialVote.GetAsync(year, 1);

        Assert.Single(players);
    }

    [Fact]
    public async Task Does_not_create_global_political_votes_or_replace_actual_votes_for_delayed_games()
    {
        var year = FantacalcioTime.ActualYear;
        _handler.Content = string.Empty;
        await _players.InsertAsync(year, new RealPlayersWrapper
        {
            Players =
            [
                new RealPlayer
                {
                    Name = "Delayed player",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Midfielder,
                    IsActive = true
                }
            ]
        }, TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(year, new RealCalendar
        {
            Year = year,
            Days =
            [
                new RealDay
                {
                    SerieADay = 1,
                    Games =
                    [
                        new RealGame
                        {
                            Home = new RealTeam { Name = "Roma" },
                            Away = new RealTeam { Name = "Milan" },
                            Delayed = true
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);

        var players = await _officialVote.GetAsync(year, 1);

        Assert.Empty(players);
    }

    private const string ValidOfficialVotesHtml = """
        <ul class="teams">
            <a class="team-name" content="roma"></a>
            <tbody>
                <div class="player-item">
                    <span>Mario Rossi</span>
                    <span class="role" data-value="a"></span>
                    <td></td>
                    <td><span data-value="6,5">6,5</span></td>
                    <td>
                        <span data-value="1"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                        <span data-value="0"></span>
                    </td>
                </div>
            </tbody>
        </ul>
        """;
}

public sealed class TestOfficialVoteHandler : HttpMessageHandler
{
    public string Content { get; set; } = string.Empty;
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(Content)
        });
}
