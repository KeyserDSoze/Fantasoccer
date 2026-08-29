namespace Fantasoccer.Business;

public interface IMarketManager
{
    ValueTask<MarketWrapper?> GetAsync(LeagueKey key, string requester, CancellationToken cancellationToken = default);
    ValueTask<MarketOperationResult> CreateAsync(LeagueKey key, Market proposal, string requester, CancellationToken cancellationToken = default);
    ValueTask<MarketOperationResult> ApproveAsync(LeagueKey key, string marketId, string requester, CancellationToken cancellationToken = default);
    ValueTask<MarketOperationResult> DenyAsync(LeagueKey key, string marketId, string requester, CancellationToken cancellationToken = default);
    ValueTask<MarketOperationResult> CancelAsync(LeagueKey key, string marketId, string requester, CancellationToken cancellationToken = default);
}
