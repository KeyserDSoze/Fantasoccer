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
        await NotifyOwnerAsync(context, market.Seller, body, market.Id, key, cancellationToken);
    }

    public async ValueTask NotifyMarketChangedAsync(
        LeagueKey key,
        Market market,
        MarketNotificationEvent notificationEvent,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(key, cancellationToken);
        if (context == null)
            return;

        var body = BuildUpdateBody(context, market, notificationEvent, actor);
        foreach (var email in context.AllEmails.Where(email => !Same(email, actor)))
            await NotifyEmailAsync(email, body, market.Id, key, cancellationToken);
    }

    private async ValueTask NotifyOwnerAsync(
        MarketNotificationContext context,
        string owner,
        string body,
        string marketId,
        LeagueKey key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || !context.OwnerEmails.TryGetValue(owner, out var emails))
            return;
        foreach (var email in emails)
            await NotifyEmailAsync(email, body, marketId, key, cancellationToken);
    }

    private async ValueTask NotifyEmailAsync(string email, string body, string marketId, LeagueKey key, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(email, cancellationToken);
        if (settings?.MarketEvents != true)
            return;

        await _pushNotificationService.SendNotificationAsync(email, settings, new Notification
        {
            Title = "FantaZone",
            Body = body,
            Icon = "https://fanta.zone/icon.png",
            Badge = "https://fanta.zone/adaptive-icon.png",
            Tag = $"market-{marketId}",
            Data = new NotificationData
            {
                Uri = $"https://fanta.zone/market/trades?groupId={Uri.EscapeDataString(key.Group)}&leagueId={Uri.EscapeDataString(key.League)}&year={key.Year}"
            }
        });
    }

    private static string BuildUpdateBody(
        MarketNotificationContext context,
        Market market,
        MarketNotificationEvent notificationEvent,
        string actor)
    {
        var buyerName = context.TeamName(market.Buyer);
        var sellerName = context.TeamName(market.Seller);
        var actorName = context.TeamName(context.OwnerFor(actor) ?? actor);

        if (market.Status != MarketStatus.Pending)
        {
            var result = market.Status switch
            {
                MarketStatus.Approved => $"Lo scambio tra {buyerName} e {sellerName} è stato completato.",
                MarketStatus.Denied => $"La proposta tra {buyerName} e {sellerName} è stata rifiutata.",
                MarketStatus.Cancelled => $"La proposta tra {buyerName} e {sellerName} è stata annullata.",
                MarketStatus.NoMoney => $"Lo scambio tra {buyerName} e {sellerName} non è stato completato: fondi insufficienti.",
                MarketStatus.NoPlayers => $"Lo scambio tra {buyerName} e {sellerName} non è stato completato: i giocatori non sono più disponibili.",
                MarketStatus.Expired => $"La proposta tra {buyerName} e {sellerName} è scaduta.",
                _ => $"La proposta tra {buyerName} e {sellerName} è stata aggiornata."
            };
            return $"[{context.LeagueName}] {result}";
        }

        var update = notificationEvent switch
        {
            MarketNotificationEvent.SellerAccepted =>
                $"{sellerName} ha accettato lo scambio con {buyerName}. Votazione aperta: {market.Approvers.Count} sì, {market.Deniers.Count} no.",
            MarketNotificationEvent.SellerRejected =>
                $"{sellerName} ha rifiutato la proposta di {buyerName}.",
            MarketNotificationEvent.VoteCast =>
                $"{actorName} ha votato {(market.Deniers.Any(x => Same(x, context.OwnerFor(actor) ?? actor)) ? "contro" : "a favore")} lo scambio ({market.Approvers.Count} sì, {market.Deniers.Count} no).",
            MarketNotificationEvent.Cancelled =>
                $"{actorName} ha annullato la proposta tra {buyerName} e {sellerName}.",
            _ => $"La proposta tra {buyerName} e {sellerName} è stata aggiornata."
        };
        return $"[{context.LeagueName}] {update}";
    }

    private async ValueTask<MarketNotificationContext?> BuildContextAsync(LeagueKey key, CancellationToken cancellationToken)
    {
        var group = await _groups.GetAsync(key.Group, cancellationToken);
        var league = group?.Leagues?.FirstOrDefault(x => x.Id == key.League);
        if (group == null || league == null || !league.IsMain)
            return null;

        var basketIds = league.BasketsId?.ToHashSet() ?? [];
        var ownerEmails = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ownerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ownerByEmail = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var basket in group.Baskets?.Where(x => basketIds.Contains(x.Id)) ?? [])
        {
            foreach (var year in basket.Years?.Where(x => x.Year == key.Year) ?? [])
            {
                foreach (var team in year.Teams ?? [])
                {
                    if (string.IsNullOrWhiteSpace(team.Owner))
                        continue;

                    string[] emails = team.AdditionalOwners != null
                        ? [team.Owner, .. team.AdditionalOwners]
                        : [team.Owner];
                    ownerEmails[team.Owner] = emails
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    ownerNames[team.Owner] = team.Name;
                    foreach (var email in emails)
                        if (!string.IsNullOrWhiteSpace(email))
                            ownerByEmail.TryAdd(email, team.Owner);
                }
            }
        }

        var allEmails = ownerEmails.Values
            .SelectMany(emails => emails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MarketNotificationContext(league.Name, ownerEmails, ownerNames, ownerByEmail, allEmails);
    }

    private static bool Same(string? first, string? second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private sealed record MarketNotificationContext(
        string LeagueName,
        Dictionary<string, List<string>> OwnerEmails,
        Dictionary<string, string> OwnerNames,
        Dictionary<string, string> OwnerByEmail,
        List<string> AllEmails)
    {
        public string? OwnerFor(string email)
            => OwnerByEmail.TryGetValue(email, out var owner) ? owner : null;

        public string TeamName(string owner)
            => OwnerNames.TryGetValue(owner, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Una squadra";
    }
}
