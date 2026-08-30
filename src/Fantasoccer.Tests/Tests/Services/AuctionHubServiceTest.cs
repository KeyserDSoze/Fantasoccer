using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Services;

[Collection("InMemoryRepository")]
public sealed class AuctionHubServiceTest
{
    private readonly AuctionHubService _service;

    public AuctionHubServiceTest(AuctionHubService service)
    {
        _service = service;
    }

    [Fact]
    public async Task Get_current_wrapper_returns_null_when_auction_does_not_exist()
    {
        var wrapper = await _service.GetCurrentWrapperAsync("missing-auction");

        Assert.Null(wrapper);
    }

    [Fact]
    public async Task Execute_operation_does_not_invoke_callback_for_missing_auction()
    {
        var invoked = false;

        await _service.ExecuteOperationAsync("missing-auction", _ => invoked = true);

        Assert.False(invoked);
    }

    [Fact]
    public async Task Execute_operation_returns_null_for_missing_auction()
    {
        var result = await _service.ExecuteOperationAsync(
            "missing-auction",
            _ => Task.FromResult("unexpected"));

        Assert.Null(result);
    }
}
