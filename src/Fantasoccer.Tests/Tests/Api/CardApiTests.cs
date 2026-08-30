using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class CardApiTests
{
    private readonly HttpClient _client;

    public CardApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_all_cards_requires_authentication()
    {
        var response = await _client.GetAsync("/Card/GetAllCards", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_all_cards_returns_empty_list_when_no_cards_exist()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync("/Card/GetAllCards", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_card_returns_not_found_for_unknown_id()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync("/Card/GetCardById/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_card_requires_super_admin_role()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.PostAsync("/Card/CreateCard", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_card_rejects_missing_name_and_description()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "admin@example.com");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "super_admin");

        var response = await _client.PostAsync(
            "/Card/CreateCard",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
