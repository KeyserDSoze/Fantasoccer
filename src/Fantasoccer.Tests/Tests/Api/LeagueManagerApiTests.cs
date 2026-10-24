using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class LeagueManagerApiTests
{
    private readonly HttpClient _client;

    public LeagueManagerApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_calendar_requires_authentication()
    {
        var response = await _client.GetAsync(
            "/LeagueManager/CreateCalendarAndRank?groupId=missing&leagueId=missing&year=2026",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_calendar_returns_false_for_unknown_group()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "super_admin");

        var response = await _client.GetAsync(
            "/LeagueManager/CreateCalendarAndRank?groupId=missing&leagueId=missing&year=2026",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("false", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
