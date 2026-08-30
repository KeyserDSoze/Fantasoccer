using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class ScalarRepositoryCrudApiTests
{
    private readonly HttpClient _client;

    public ScalarRepositoryCrudApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("RealCalendar", "202699")]
    [InlineData("RealRank", "202699")]
    [InlineData("StatPlayersWrapper", "202699")]
    public async Task Scalar_repository_get_returns_empty_for_missing_key(string repository, string key)
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "scalar@example.com");

        var response = await _client.GetAsync(
            $"/api/{repository}/Get?key={key}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Real_players_repository_keeps_rystem_missing_key_behavior()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "scalar@example.com");

        var response = await _client.GetAsync(
            "/api/RealPlayersWrapper/Get?key=202699",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

}
