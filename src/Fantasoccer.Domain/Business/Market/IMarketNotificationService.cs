namespace Fantasoccer.Business;

public interface IMarketNotificationService
{
    ValueTask NotifyProposalCreatedAsync(LeagueKey key, Market market, CancellationToken cancellationToken = default);
    ValueTask NotifyMarketChangedAsync(
        LeagueKey key,
        Market market,
        MarketNotificationEvent notificationEvent,
        string actor,
        CancellationToken cancellationToken = default);
}

public enum MarketNotificationEvent
{
    SellerAccepted,
    SellerRejected,
    VoteCast,
    Cancelled,
    Expired
}
