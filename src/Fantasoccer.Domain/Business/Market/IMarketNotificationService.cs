namespace Fantasoccer.Business;

public interface IMarketNotificationService
{
    ValueTask NotifyProposalCreatedAsync(LeagueKey key, Market market, CancellationToken cancellationToken = default);
    ValueTask NotifyOutcomeAsync(LeagueKey key, Market market, CancellationToken cancellationToken = default);
}
