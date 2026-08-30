using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class SettingsApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SettingsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Update_username_requires_authentication()
    {
        var response = await _client.GetAsync(
            "/Settings/UpdateUsername?newUsername=valid_name",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_username_returns_false_when_identity_does_not_exist()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "missing@example.com");

        var response = await _client.GetAsync(
            "/Settings/UpdateUsername?newUsername=valid_name",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("false", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_username_rejects_invalid_characters()
    {
        const string email = "settings-invalid@example.com";
        await AddIdentityAsync(email);
        _client.DefaultRequestHeaders.Add("X-Test-User", email);

        var response = await _client.GetAsync(
            "/Settings/UpdateUsername?newUsername=not-valid",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("false", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_username_updates_valid_identity()
    {
        const string email = "settings-valid@example.com";
        var identity = await AddIdentityAsync(email);
        _client.DefaultRequestHeaders.Add("X-Test-User", email);

        var response = await _client.GetAsync(
            "/Settings/UpdateUsername?newUsername=valid_name",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updated = await _factory.Services.GetRequiredService<IRepository<AppIdentity, string>>().GetAsync(email, TestContext.Current.CancellationToken);
        Assert.Equal("valid_name", updated?.Username);
    }

    private async Task<AppIdentity> AddIdentityAsync(string email)
    {
        var identity = new AppIdentity { Email = email, Groups = [] };
        await _factory.Services.GetRequiredService<IRepository<AppIdentity, string>>().InsertAsync(
            email,
            identity,
            TestContext.Current.CancellationToken);
        return identity;
    }
}
