using System.Net;
using System.Text;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class RepositoryApiTests
{
    private readonly HttpClient _client;

    public RepositoryApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Swagger_describes_repository_endpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Group", body);
        Assert.Contains("RealCalendar", body);
        Assert.Contains("Team", body);
    }

    [Theory]
    [InlineData("/api/Group/Get?key=unknown")]
    [InlineData("/api/RealCalendar/Get?key=2026")]
    [InlineData("/api/RealRank/Get?key=2026")]
    [InlineData("/api/RealPlayersWrapper/Get?key=2026")]
    [InlineData("/api/StatPlayersWrapper/Get?key=2026")]
    public async Task Repository_get_requires_authentication(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Team", "{\"Group\":\"missing\",\"BasketId\":\"missing\",\"Email\":\"missing@example.com\",\"Year\":2026}")]
    [InlineData("Calendar", "{\"Group\":\"missing\",\"League\":\"missing\",\"Year\":2026}")]
    [InlineData("Rank", "{\"Group\":\"missing\",\"League\":\"missing\",\"Year\":2026}")]
    [InlineData("HallOfFame", "{\"Group\":\"missing\",\"League\":\"missing\"}")]
    public async Task Composite_repository_get_requires_authentication(string repository, string key)
    {
        var response = await _client.GetAsync(
            $"/api/{repository}/Get?key={Uri.EscapeDataString(key)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task All_exposed_repository_operations_require_authentication()
    {
        var repositories = new[]
        {
            "AppIdentity", "Calendar", "CardWrapper", "ChancedRealPlayerWrapper", "Group",
            "HallOfFame", "LiveGroup", "PlayedGame", "PushNotificationSent", "PushNotificationSettings",
            "Rank", "RealCalendar", "RealPlayersWrapper", "StatPlayersWrapper", "Team", "VotedRealPlayerWrapper"
        };
        var operations = new[] { "Get", "Insert", "Update", "Delete", "Query", "Exist", "Batch", "Bootstrap" };

        foreach (var repository in repositories)
        foreach (var operation in operations)
        {
            var method = operation is "Get" or "Delete" or "Exist" or "Bootstrap" ? HttpMethod.Get : HttpMethod.Post;
            using var request = new HttpRequestMessage(method, $"/api/{repository}/{operation}?key=missing");
            if (method == HttpMethod.Post)
                request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                continue;
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Unknown_repository_path_returns_not_found()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync(
            "/api/not-a-repository/unknown",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Group_repository_supports_insert_and_get()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "repository@example.com");
        const string key = "integration-group";

        var insert = await _client.PostAsync(
            $"/api/Group/Insert?key={key}",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        insert.EnsureSuccessStatusCode();

        var get = await _client.GetAsync(
            $"/api/Group/Get?key={key}",
            TestContext.Current.CancellationToken);

        get.EnsureSuccessStatusCode();
        Assert.Equal(
            "{\"i\":null,\"n\":null,\"l\":null,\"u\":null,\"b\":null}",
            await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Group_repository_supports_update_exist_query_and_delete()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "repository-crud@example.com");
        const string key = "integration-group-crud";
        const string group = "{\"i\":\"integration-group-crud\",\"n\":\"CRUD Group\",\"l\":[],\"u\":[],\"b\":[]}";

        var insert = await _client.PostAsync(
            $"/api/Group/Insert?key={key}",
            new StringContent(group, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        insert.EnsureSuccessStatusCode();

        var update = await _client.PostAsync(
            $"/api/Group/Update?key={key}",
            new StringContent(group.Replace("CRUD Group", "Updated Group"), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        update.EnsureSuccessStatusCode();

        var exists = await _client.GetAsync(
            $"/api/Group/Exist?key={key}",
            TestContext.Current.CancellationToken);
        exists.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await exists.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var query = await _client.PostAsync(
            "/api/Group/Query",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        query.EnsureSuccessStatusCode();

        var delete = await _client.GetAsync(
            $"/api/Group/Delete?key={key}",
            TestContext.Current.CancellationToken);
        delete.EnsureSuccessStatusCode();

        var afterDelete = await _client.GetAsync(
            $"/api/Group/Get?key={key}",
            TestContext.Current.CancellationToken);
        afterDelete.EnsureSuccessStatusCode();
    }
}
