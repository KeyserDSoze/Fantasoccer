using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class MarketApiTests
{
    private readonly HttpClient _client;

    public MarketApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_market_requires_authentication()
    {
        var response = await _client.GetAsync(
            "/Market/Get?groupId=missing&year=2026&leagueId=missing",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_market_forbids_authenticated_user_without_access()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync(
            "/Market/Get?groupId=missing&year=2026&leagueId=missing",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
