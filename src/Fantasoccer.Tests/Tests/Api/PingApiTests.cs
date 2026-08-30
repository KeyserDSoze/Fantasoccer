using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class PingApiTests
{
    private readonly HttpClient _client;

    public PingApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_requires_authentication()
    {
        var response = await _client.GetAsync("/Ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_returns_true_for_authenticated_user()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync("/Ping", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
