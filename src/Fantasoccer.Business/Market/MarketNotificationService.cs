using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class MarketNotificationService : IMarketNotificationService
{
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<PushNotificationSettings, string> _settings;
    private readonly IPushNotificationService _pushNotificationService;

    public MarketNotificationService(
        IRepository<Group, string> groups,
        IRepository<PushNotificationSettings, string> settings,
        IPushNotificationService pushNotificationService)
    {
        _groups = groups;
        _settings = settings;
        _pushNotificationService = pushNotificationService;
    }

    public async ValueTask NotifyProposalCreatedAsync(LeagueKey key, Market market, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(key, cancellationToken);
        if (context == null)
            return;
        var buyerName = context.TeamName(market.Buyer);
        var body = $"[{context.LeagueName}] {buyerName} ti ha proposto uno scambio.";
        await NotifyOwnerAsync(context, market.Seller, body, market.Id, cancellationToken);
    }

    public async ValueTask NotifyOutcomeAsync(LeagueKey key, Market market, CancellationToken cancellationToken = default)
    {
        if (market.Status != MarketStatus.Approved && market.Status != MarketStatus.Denied)
            return;
        var context = await BuildContextAsync(key, cancellationToken);
        if (context == null)
            return;
        var buyerName = context.TeamName(market.Buyer);
        var sellerName = context.TeamName(market.Seller);
        var body = market.Status == MarketStatus.Approved
            ? $"[{context.LeagueName}] Scambio tra {buyerName} e {sellerName} completato!"
            : $"[{context.LeagueName}] La proposta di scambio tra {buyerName} e {sellerName} è stata rifiutata.";
        await NotifyOwnerAsync(context, market.Buyer, body, market.Id, cancellationToken);
        await NotifyOwnerAsync(context, market.Seller, body, market.Id, cancellationToken);
    }

    private async ValueTask NotifyOwnerAsync(MarketNotificationContext context, string owner, string body, string marketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || !context.OwnerEmails.TryGetValue(owner, out var emails))
            return;
        foreach (var email in emails)
        {
            var settings = await _settings.GetAsync(email, cancellationToken);
            if (settings?.MarketEvents != true)
                continue;
            await _pushNotificationService.SendNotificationAsync(email, settings, new Notification
            {
                Title = "FantaZone",
                Body = body,
                Icon = "https://fanta.zone/icon.png",
                Badge = "https://fanta.zone/adaptive-icon.png",
                Tag = $"market-{marketId}",
                Data = new NotificationData
                {
                    Uri = "https://fanta.zone/market"
                }
            });
        }
    }

    private async ValueTask<MarketNotificationContext?> BuildContextAsync(LeagueKey key, CancellationToken cancellationToken)
    {
        var group = await _groups.GetAsync(key.Group, cancellationToken);
        var league = group?.Leagues?.FirstOrDefault(x => x.Id == key.League);
        if (group == null || league == null)
            return null;
        var basketIds = league.BasketsId?.ToHashSet() ?? [];
        var ownerEmails = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ownerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var basket in group.Baskets?.Where(x => basketIds.Contains(x.Id)) ?? [])
        {
            foreach (var year in basket.Years?.Where(x => x.Year == key.Year) ?? [])
            {
                foreach (var team in year.Teams ?? [])
                {
                    if (string.IsNullOrWhiteSpace(team.Owner))
                        continue;
                    ownerEmails[team.Owner] = team.AdditionalOwners != null
                        ? [team.Owner, .. team.AdditionalOwners]
                        : [team.Owner];
                    ownerNames[team.Owner] = team.Name;
                }
            }
        }
        return new MarketNotificationContext(league.Name, ownerEmails, ownerNames);
    }

    private sealed record MarketNotificationContext(
        string LeagueName,
        Dictionary<string, List<string>> OwnerEmails,
        Dictionary<string, string> OwnerNames)
    {
        public string TeamName(string owner)
            => OwnerNames.TryGetValue(owner, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Una squadra";
    }
}
