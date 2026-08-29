using System.Collections.Concurrent;
using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class MarketManager : IMarketManager
{
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<MarketWrapper, LeagueKey> _markets;
    private readonly IRepository<Team, TeamKey> _teams;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public MarketManager(
        IRepository<Group, string> groups,
        IRepository<MarketWrapper, LeagueKey> markets,
        IRepository<Team, TeamKey> teams)
    {
        _groups = groups;
        _markets = markets;
        _teams = teams;
    }

    public async ValueTask<MarketWrapper?> GetAsync(
        LeagueKey key,
        string requester,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(key, cancellationToken);
        if (context == null || (!context.Owners.Contains(requester)
            && context.Group.Users?.Any(x => Same(x.Email, requester) && x.Role != IdentityRole.None) != true))
            return null;
        return await _markets.GetAsync(key, cancellationToken) ?? new MarketWrapper { Markets = [] };
    }

    public ValueTask<MarketOperationResult> CreateAsync(
        LeagueKey key,
        Market proposal,
        string requester,
        CancellationToken cancellationToken = default)
        => LockedAsync(key, async () =>
        {
            if (key.Year != FantacalcioTime.ActualYear)
                return Failed("Il mercato è disponibile solo per la stagione corrente.");
            var context = await GetContextAsync(key, cancellationToken);
            if (context == null)
                return Failed("Lega non valida.");
            if (context.Settings.Market == MarketType.Denied)
                return Failed("Il mercato non è abilitato per questa lega.");
            if (!Same(requester, proposal.Buyer))
                return Failed("Solo il compratore può creare la proposta.");
            if (Same(proposal.Buyer, proposal.Seller) || !context.Owners.Contains(proposal.Buyer) || !context.Owners.Contains(proposal.Seller))
                return Failed("Compratore o venditore non validi.");
            if (proposal.MoneyFromBuyer < 0 || proposal.MoneyFromSeller < 0)
                return Failed("Gli importi non possono essere negativi.");

            var teams = await GetTeamsAsync(key, context, proposal.Buyer, proposal.Seller, cancellationToken);
            if (teams == null)
                return Failed("Squadra non trovata.");
            var buyerPlayers = FindPlayers(teams.Value.Buyer, proposal.BuyerPlayers);
            var sellerPlayers = FindPlayers(teams.Value.Seller, proposal.SellerPlayers);
            if (buyerPlayers == null || sellerPlayers == null || buyerPlayers.Count == 0 || sellerPlayers.Count == 0)
                return Failed("Uno o più giocatori non sono disponibili.");
            if (!HaveSameRoles(buyerPlayers, sellerPlayers))
                return Failed("Lo scambio deve avere lo stesso numero di giocatori per ruolo.");

            var market = new Market
            {
                Id = Guid.NewGuid().ToString(),
                Buyer = proposal.Buyer,
                Seller = proposal.Seller,
                BuyerPlayers = buyerPlayers.Select(ClonePlayer).ToList(),
                SellerPlayers = sellerPlayers.Select(ClonePlayer).ToList(),
                MoneyFromBuyer = proposal.MoneyFromBuyer,
                MoneyFromSeller = proposal.MoneyFromSeller,
                Approvers = [proposal.Buyer, proposal.Seller],
                Deniers = [],
                Status = MarketStatus.Pending,
                CreationTime = DateTime.UtcNow
            };
            var wrapper = await _markets.GetAsync(key, cancellationToken) ?? new MarketWrapper { Markets = [] };
            wrapper.Markets ??= [];
            wrapper.Markets.Add(market);
            await SaveMarketsAsync(key, wrapper, cancellationToken);

            if (context.Settings.Market == MarketType.WithoutVote || HasQuorum(market.Approvers.Count, context.TeamCount))
            {
                await ExecuteAsync(key, context, market, cancellationToken);
                await _markets.UpdateAsync(key, wrapper, cancellationToken);
            }
            return ResultFor(market);
        }, cancellationToken);

    public ValueTask<MarketOperationResult> ApproveAsync(
        LeagueKey key,
        string marketId,
        string requester,
        CancellationToken cancellationToken = default)
        => VoteAsync(key, marketId, requester, true, cancellationToken);

    public ValueTask<MarketOperationResult> DenyAsync(
        LeagueKey key,
        string marketId,
        string requester,
        CancellationToken cancellationToken = default)
        => VoteAsync(key, marketId, requester, false, cancellationToken);

    public ValueTask<MarketOperationResult> CancelAsync(
        LeagueKey key,
        string marketId,
        string requester,
        CancellationToken cancellationToken = default)
        => LockedAsync(key, async () =>
        {
            if (key.Year != FantacalcioTime.ActualYear)
                return Failed("Il mercato è disponibile solo per la stagione corrente.");
            var wrapper = await _markets.GetAsync(key, cancellationToken);
            var market = wrapper?.Markets?.FirstOrDefault(x => x.Id == marketId);
            if (market == null || market.Status != MarketStatus.Pending)
                return Failed("Proposta non modificabile.", market);
            if (!Same(requester, market.Buyer) && !Same(requester, market.Seller))
                return Failed("Solo le squadre coinvolte possono annullare la proposta.", market);
            market.Status = MarketStatus.Cancelled;
            await _markets.UpdateAsync(key, wrapper!, cancellationToken);
            return Successful(market);
        }, cancellationToken);

    private ValueTask<MarketOperationResult> VoteAsync(
        LeagueKey key,
        string marketId,
        string requester,
        bool approve,
        CancellationToken cancellationToken)
        => LockedAsync(key, async () =>
        {
            if (key.Year != FantacalcioTime.ActualYear)
                return Failed("Il mercato è disponibile solo per la stagione corrente.");
            var context = await GetContextAsync(key, cancellationToken);
            if (context == null || context.Settings.Market != MarketType.WithVote)
                return Failed("La lega non prevede la votazione del mercato.");
            var wrapper = await _markets.GetAsync(key, cancellationToken);
            var market = wrapper?.Markets?.FirstOrDefault(x => x.Id == marketId);
            if (market == null || market.Status != MarketStatus.Pending)
                return Failed("Proposta non modificabile.", market);
            if (!context.Owners.Contains(requester) || Same(requester, market.Buyer) || Same(requester, market.Seller))
                return Failed("Utente non abilitato alla votazione.", market);

            market.Approvers.RemoveAll(x => Same(x, requester));
            market.Deniers.RemoveAll(x => Same(x, requester));
            (approve ? market.Approvers : market.Deniers).Add(requester);
            if (HasQuorum(market.Deniers.Count, context.TeamCount))
                market.Status = MarketStatus.Denied;
            else if (HasQuorum(market.Approvers.Count, context.TeamCount))
                await ExecuteAsync(key, context, market, cancellationToken);
            await _markets.UpdateAsync(key, wrapper!, cancellationToken);
            return ResultFor(market);
        }, cancellationToken);

    private async ValueTask ExecuteAsync(LeagueKey key, MarketContext context, Market market, CancellationToken cancellationToken)
    {
        var teams = await GetTeamsAsync(key, context, market.Buyer, market.Seller, cancellationToken);
        if (teams == null)
        {
            market.Status = MarketStatus.NoPlayers;
            return;
        }
        var buyerPlayers = FindPlayers(teams.Value.Buyer, market.BuyerPlayers);
        var sellerPlayers = FindPlayers(teams.Value.Seller, market.SellerPlayers);
        if (buyerPlayers == null || sellerPlayers == null)
        {
            market.Status = MarketStatus.NoPlayers;
            return;
        }

        var buyerBudget = Budget(teams.Value.Buyer, context.Settings)
            + buyerPlayers.Sum(x => x.Price) - sellerPlayers.Sum(x => x.Price)
            - market.MoneyFromBuyer + market.MoneyFromSeller;
        var sellerBudget = Budget(teams.Value.Seller, context.Settings)
            + sellerPlayers.Sum(x => x.Price) - buyerPlayers.Sum(x => x.Price)
            - market.MoneyFromSeller + market.MoneyFromBuyer;
        if (buyerBudget < 0 || sellerBudget < 0)
        {
            market.Status = MarketStatus.NoMoney;
            return;
        }

        var buyerKeys = buyerPlayers.Select(x => x.Key).ToHashSet();
        var sellerKeys = sellerPlayers.Select(x => x.Key).ToHashSet();
        AssignIncomingPositions(buyerPlayers, sellerPlayers);
        teams.Value.Buyer.Players.RemoveAll(x => buyerKeys.Contains(x.Key));
        teams.Value.Seller.Players.RemoveAll(x => sellerKeys.Contains(x.Key));
        teams.Value.Buyer.Players.AddRange(sellerPlayers);
        teams.Value.Seller.Players.AddRange(buyerPlayers);
        teams.Value.Buyer.MoneyFromRank += market.MoneyFromSeller - market.MoneyFromBuyer;
        teams.Value.Seller.MoneyFromRank += market.MoneyFromBuyer - market.MoneyFromSeller;
        await _teams.UpdateAsync(teams.Value.BuyerKey, teams.Value.Buyer, cancellationToken);
        await _teams.UpdateAsync(teams.Value.SellerKey, teams.Value.Seller, cancellationToken);
        market.Status = MarketStatus.Approved;
    }

    private async ValueTask<MarketContext?> GetContextAsync(LeagueKey key, CancellationToken cancellationToken)
    {
        var group = await _groups.GetAsync(key.Group, cancellationToken);
        var league = group?.Leagues?.FirstOrDefault(x => x.Id == key.League);
        var annualLeague = league?.Years?.FirstOrDefault(x => x.Year == key.Year);
        if (group == null || league == null || annualLeague == null)
            return null;
        var basketIds = league.BasketsId?.ToHashSet() ?? [];
        var annualTeams = group.Baskets
            .Where(x => basketIds.Contains(x.Id))
            .SelectMany(basket => basket.Years
                .Where(year => year.Year == key.Year)
                .SelectMany(year => (year.Teams ?? []).Select(team => (Team: team, BasketId: basket.Id))))
            .Where(x => !string.IsNullOrWhiteSpace(x.Team.Owner))
            .ToList();
        var teamBaskets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in annualTeams)
            teamBaskets.TryAdd(team.Team.Owner, team.BasketId);
        return new MarketContext(
            group,
            annualLeague.Settings ?? LeagueSetting.Default,
            teamBaskets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            teamBaskets,
            annualTeams.Count);
    }

    private async ValueTask<(Team Buyer, Team Seller, TeamKey BuyerKey, TeamKey SellerKey)?> GetTeamsAsync(
        LeagueKey key,
        MarketContext context,
        string buyer,
        string seller,
        CancellationToken cancellationToken)
    {
        if (!context.TeamBaskets.TryGetValue(buyer, out var buyerBasket)
            || !context.TeamBaskets.TryGetValue(seller, out var sellerBasket))
            return null;
        var buyerKey = new TeamKey { Group = key.Group, BasketId = buyerBasket, Email = buyer, Year = key.Year };
        var sellerKey = new TeamKey { Group = key.Group, BasketId = sellerBasket, Email = seller, Year = key.Year };
        var buyerTeam = await _teams.GetAsync(buyerKey, cancellationToken);
        var sellerTeam = await _teams.GetAsync(sellerKey, cancellationToken);
        return buyerTeam == null || sellerTeam == null ? null : (buyerTeam, sellerTeam, buyerKey, sellerKey);
    }

    private async ValueTask SaveMarketsAsync(LeagueKey key, MarketWrapper wrapper, CancellationToken cancellationToken)
    {
        if (await _markets.ExistAsync(key, cancellationToken))
            await _markets.UpdateAsync(key, wrapper, cancellationToken);
        else
            await _markets.InsertAsync(key, wrapper, cancellationToken);
    }

    private async ValueTask<MarketOperationResult> LockedAsync(
        LeagueKey key,
        Func<ValueTask<MarketOperationResult>> action,
        CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd($"{key.Group}:{key.League}:{key.Year}", _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static List<Player>? FindPlayers(Team team, IEnumerable<Player>? requested)
    {
        var keys = requested?.Select(x => x.Key).ToList() ?? [];
        if (keys.Count != keys.Distinct().Count())
            return null;
        var players = keys.Select(key => team.ActivePlayers.FirstOrDefault(x => x.Key == key)).ToList();
        return players.Any(x => x == null) ? null : players.OfType<Player>().ToList();
    }

    private static bool HaveSameRoles(IEnumerable<Player> buyerPlayers, IEnumerable<Player> sellerPlayers)
        => buyerPlayers.GroupBy(x => x.Role).ToDictionary(x => x.Key, x => x.Count())
            .OrderBy(x => x.Key)
            .SequenceEqual(sellerPlayers.GroupBy(x => x.Role).ToDictionary(x => x.Key, x => x.Count()).OrderBy(x => x.Key));

    private static int Budget(Team team, LeagueSetting settings)
        => settings.StartingMoney - team.Cost;

    private static void AssignIncomingPositions(List<Player> buyerPlayers, List<Player> sellerPlayers)
    {
        foreach (var role in buyerPlayers.Select(x => x.Role).Distinct())
        {
            var buyerByRole = buyerPlayers.Where(x => x.Role == role).ToList();
            var sellerByRole = sellerPlayers.Where(x => x.Role == role).ToList();
            for (var index = 0; index < buyerByRole.Count; index++)
                (buyerByRole[index].Position, sellerByRole[index].Position) = (sellerByRole[index].Position, buyerByRole[index].Position);
        }
    }

    private static bool HasQuorum(int votes, int teams)
        => votes >= teams / 2 + 1;

    private static bool Same(string? first, string? second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static Player ClonePlayer(Player player)
        => new()
        {
            Name = player.Name,
            RealTeam = new RealTeam { Name = player.RealTeam.Name, Abbreviation = player.RealTeam.Abbreviation },
            Role = player.Role,
            IsActive = player.IsActive,
            Visible = player.Visible,
            Price = player.Price,
            Revenue = player.Revenue,
            Status = player.Status,
            Position = player.Position
        };

    private static MarketOperationResult Successful(Market market)
        => new() { IsOk = true, Market = market };

    private static MarketOperationResult ResultFor(Market market)
        => market.Status is MarketStatus.NoMoney or MarketStatus.NoPlayers
            ? Failed("Lo scambio non può essere eseguito.", market)
            : Successful(market);

    private static MarketOperationResult Failed(string message, Market? market = null)
        => new() { IsOk = false, Message = message, Market = market };

    private sealed record MarketContext(
        Group Group,
        LeagueSetting Settings,
        HashSet<string> Owners,
        Dictionary<string, string> TeamBaskets,
        int TeamCount);
}
