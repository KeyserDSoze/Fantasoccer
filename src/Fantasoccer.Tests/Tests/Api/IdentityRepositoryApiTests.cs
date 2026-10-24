using System.Net;
using System.Text;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class IdentityRepositoryApiTests
{
    private readonly HttpClient _client;

    public IdentityRepositoryApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task App_identity_repository_supports_crud()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "identity-repository@example.com");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "super_admin");
        const string key = "identity-repository@example.com";
        const string identity = "{\"e\":\"identity-repository@example.com\",\"s\":{},\"g\":[]}";

        var insert = await _client.PostAsync($"/api/AppIdentity/Insert?key={key}", Json(identity), TestContext.Current.CancellationToken);
        insert.EnsureSuccessStatusCode();

        var get = await _client.GetAsync($"/api/AppIdentity/Get?key={key}", TestContext.Current.CancellationToken);
        get.EnsureSuccessStatusCode();
        Assert.Contains("identity-repository@example.com", await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var update = await _client.PostAsync($"/api/AppIdentity/Update?key={key}", Json(identity.Replace("{}", "{\"u\":\"identity_user\"}")), TestContext.Current.CancellationToken);
        update.EnsureSuccessStatusCode();

        var exists = await _client.GetAsync($"/api/AppIdentity/Exist?key={key}", TestContext.Current.CancellationToken);
        exists.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await exists.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var delete = await _client.GetAsync($"/api/AppIdentity/Delete?key={key}", TestContext.Current.CancellationToken);
        delete.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task App_identity_get_requires_authentication()
    {
        var response = await _client.GetAsync("/api/AppIdentity/Get?key=anonymous@example.com", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
}
