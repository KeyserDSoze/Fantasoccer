using System.Net;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;

namespace Fantasoccer.Tests.Tests.Votes;

public sealed class LiveVoteParserTest : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ILiveVote _liveVote;
    private readonly TestLiveVoteHttpHandler _handler;

    public LiveVoteParserTest()
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
        services.AddFantasoccerSqlDataProviders();
        services.AddSingleton<TestLiveVoteHttpHandler>();
        services.AddHttpClient("base")
            .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestLiveVoteHttpHandler>());

        _services = services.BuildServiceProvider();
        _liveVote = _services.GetRequiredService<ILiveVote>();
        _handler = _services.GetRequiredService<TestLiveVoteHttpHandler>();
        _handler.Protobuf = ProtobufFixture();
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task ParsesSignedProtobufResponse()
    {
        var players = await _liveVote.GetAsync(15, 1);

        Assert.Equal(2, players.Count);
        var forward = players.Single(x => x.Name == "Mario rossi");
        Assert.Equal("Roma", forward.RealTeam.Name);
        Assert.Equal(Role.Forward, forward.Role);
        Assert.True(forward.IsActive);
        Assert.Equal(7, forward.Vote.Value);
        Assert.Equal(2, forward.Vote.Goal);
        Assert.Equal(1, forward.Vote.Assist);
        Assert.Equal(Behaviour.YellowCard, forward.Vote.Status);
        Assert.True(forward.Vote.IsIn);
        Assert.True(forward.Vote.ManOfTheMatch);

        var goalkeeper = players.Single(x => x.Name == "Luca bianchi");
        Assert.Equal(Role.GoalKeeper, goalkeeper.Role);
        Assert.False(goalkeeper.Vote.HasVote);
        Assert.Equal(0, goalkeeper.Vote.Value);
        Assert.Equal(1, goalkeeper.Vote.SufferedGoal);
        Assert.Equal(1, goalkeeper.Vote.StoppedPenalty);
        Assert.True(goalkeeper.Vote.IsOut);
        Assert.Equal(Behaviour.RedCard, goalkeeper.Vote.Status);

        Assert.Equal(HttpMethod.Post, _handler.SignedMethod);
        Assert.Equal("https://www.fantacalcio.it/api/v1/SignedUri", _handler.SignedRequestUri?.ToString());
        Assert.Contains("https://api.fantacalcio.it/v1/st/21/matches/live/1.dat", _handler.SignedRequestBody);
        Assert.Equal("https://www.fantacalcio.it", _handler.Origin);
        Assert.Equal("https://www.fantacalcio.it/live-serie-a", _handler.Referer);
        Assert.Equal("https://cdn.test/live.dat", _handler.BinaryRequestUri?.ToString());
        Assert.Contains("Mozilla", _handler.UserAgent);
    }

    [Fact]
    public async Task CanBeCalledMoreThanOnceWithSameClient()
    {
        await _liveVote.GetAsync(15, 1);
        await _liveVote.GetAsync(15, 1);

        Assert.Equal(2, _handler.SignedRequests);
        Assert.Equal(2, _handler.BinaryRequests);
    }

    [Fact]
    public async Task ReturnsEmptyWhenProviderReportsNotFound()
    {
        _handler.SignedResponse = """
            {"request":{"resources":[],"errors":[{"statusDescription":"Not Found"}]}}
            """;

        var players = await _liveVote.GetAsync(15, 1);

        Assert.Empty(players);
        Assert.Equal(0, _handler.BinaryRequests);
    }

    [Fact]
    public async Task ReturnsEmptyWhenSignedUriIsMissing()
    {
        _handler.SignedResponse = "{" + "\"resources\":[]" + "}";

        var players = await _liveVote.GetAsync(15, 1);

        Assert.Empty(players);
        Assert.Equal(0, _handler.BinaryRequests);
    }

    [Fact]
    public async Task PropagatesSignedUriHttpErrors()
    {
        _handler.SignedStatusCode = HttpStatusCode.ServiceUnavailable;

        await Assert.ThrowsAsync<HttpRequestException>(() => _liveVote.GetAsync(15, 1));
    }

    private static byte[] ProtobufFixture()
    {
        var message = new LiveVoteFixtureMessage
        {
            Games =
            [
                new LiveVoteFixtureGame
                {
                    TeamHome = "Roma",
                    TeamAway = "Milan",
                    PlayersHome =
                    [
                        new LiveVoteFixturePlayer
                        {
                            Name = "mario rossi",
                            Position = "A",
                            Vote = 7,
                            Events = [3, 3, 5, 1, 15, 26]
                        },
                        new LiveVoteFixturePlayer
                        {
                            Name = "ignored",
                            Position = "ALL",
                            Vote = 8
                        }
                    ],
                    PlayersAway =
                    [
                        new LiveVoteFixturePlayer
                        {
                            Name = "luca bianchi",
                            Position = "P",
                            Vote = 55,
                            Events = [4, 7, 14, 2]
                        }
                    ]
                }
            ]
        };
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }
}

public sealed class TestLiveVoteHttpHandler : HttpMessageHandler
{
    public string SignedResponse { get; set; } = """
        {"request":{"resources":[{"signedUri":"https://cdn.test/live.dat"}],"errors":[]}}
        """;
    public HttpStatusCode SignedStatusCode { get; set; } = HttpStatusCode.OK;
    public byte[] Protobuf { get; set; } = [];
    public int SignedRequests { get; private set; }
    public int BinaryRequests { get; private set; }
    public HttpMethod? SignedMethod { get; private set; }
    public Uri? SignedRequestUri { get; private set; }
    public string SignedRequestBody { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Referer { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public Uri? BinaryRequestUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post)
        {
            SignedRequests++;
            SignedMethod = request.Method;
            SignedRequestUri = request.RequestUri;
            SignedRequestBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Origin = request.Headers.GetValues("Origin").Single();
            Referer = request.Headers.Referrer?.ToString() ?? string.Empty;
            UserAgent = request.Headers.UserAgent.ToString();
            return new HttpResponseMessage(SignedStatusCode)
            {
                Content = new StringContent(SignedResponse)
            };
        }

        BinaryRequests++;
        BinaryRequestUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Protobuf)
        };
    }
}

[ProtoContract]
public sealed class LiveVoteFixtureMessage
{
    [ProtoMember(1)]
    public List<LiveVoteFixtureGame> Games { get; set; } = [];
}

[ProtoContract]
public sealed class LiveVoteFixtureGame
{
    [ProtoMember(13)]
    public string TeamHome { get; set; } = string.Empty;
    [ProtoMember(14)]
    public string TeamAway { get; set; } = string.Empty;
    [ProtoMember(15)]
    public List<LiveVoteFixturePlayer> PlayersHome { get; set; } = [];
    [ProtoMember(16)]
    public List<LiveVoteFixturePlayer> PlayersAway { get; set; } = [];
}

[ProtoContract]
public sealed class LiveVoteFixturePlayer
{
    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;
    [ProtoMember(3)]
    public string Position { get; set; } = string.Empty;
    [ProtoMember(4)]
    public double Vote { get; set; }
    [ProtoMember(5)]
    public List<int> Events { get; set; } = [];
}
