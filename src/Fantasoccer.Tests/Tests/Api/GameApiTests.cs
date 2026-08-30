using System.Net;
using System.Text;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class GameApiTests
{
    private readonly HttpClient _client;

    public GameApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Save_team_requires_authentication()
    {
        var response = await _client.PostAsync(
            "/Game/SaveTeam?groupId=missing",
            new StringContent("{\"n\":\"Test\",\"o\":\"missing@example.com\",\"a\":[],\"p\":[]}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Save_team_rejects_unknown_authenticated_user()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "missing@example.com");

        var response = await _client.PostAsync(
            "/Game/SaveTeam?groupId=missing",
            new StringContent("{\"n\":\"Test\",\"o\":\"missing@example.com\",\"a\":[],\"p\":[]}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Utente non valido", body);
    }
}
