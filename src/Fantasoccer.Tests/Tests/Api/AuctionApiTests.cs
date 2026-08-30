using System.Net;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class AuctionApiTests
{
    private readonly HttpClient _client;

    public AuctionApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_auction_requires_authentication()
    {
        var response = await _client.GetAsync(
            "/Auction/GetAuction?auctionId=missing",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_auction_returns_null_for_unknown_auction()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "test@example.com");

        var response = await _client.GetAsync(
            "/Auction/GetAuction?auctionId=missing",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
