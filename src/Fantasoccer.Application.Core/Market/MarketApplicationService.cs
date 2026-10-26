using System.Security.Claims;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Market;

public sealed class MarketApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions, TeamRosterAvailabilityService availability, TimeProvider clock)
{
    public async Task<MarketDto[]> ListAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        return (await Query(group, league, year).AsNoTracking().OrderBy(x => x.Ordinal).ToArrayAsync(cancellationToken)).Select(Dto).ToArray();
    }

    public async Task<MarketOperationDto> CreateAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CreateMarketRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var (rules, teams, actorId) = await ContextAsync(caller, group, league, year, cancellationToken);
        if (rules.Market == (int)MarketType.Denied) throw Invalid("Il mercato non è abilitato per questa lega.");
        var buyer = teams.SingleOrDefault(x => x.Id == request.BuyerTeamId) ?? throw Invalid("Compratore non presente nella competizione.");
        var seller = teams.SingleOrDefault(x => x.Id == request.SellerTeamId) ?? throw Invalid("Venditore non presente nella competizione.");
        if (!buyer.Owners.Any(x => x.UserId == actorId)) throw Denied();
        if (buyer.Id == seller.Id || request.MoneyFromBuyer < 0 || request.MoneyFromSeller < 0) throw Invalid("Squadre o importi non validi.");
        var buyerRoster = await RosterAsync(group, year, buyer.Id, cancellationToken);
        var sellerRoster = await RosterAsync(group, year, seller.Id, cancellationToken);
        if (buyerRoster.Version != request.ExpectedBuyerRosterVersion || sellerRoster.Version != request.ExpectedSellerRosterVersion) throw Changed();
        var outgoingBuyer = FindPlayers(buyerRoster, request.BuyerPlayerIds) ?? throw Invalid("Giocatori del compratore non disponibili.");
        var outgoingSeller = FindPlayers(sellerRoster, request.SellerPlayerIds) ?? throw Invalid("Giocatori del venditore non disponibili.");
        if (outgoingBuyer.Length == 0 || outgoingSeller.Length == 0 || !SameRoles(outgoingBuyer, outgoingSeller) ||
            outgoingBuyer.Select(x => x.PlayerId).Intersect(outgoingSeller.Select(x => x.PlayerId)).Any()) throw Invalid("Lo scambio richiede lo stesso numero di giocatori per ruolo e identità distinte.");
        if (!await database.MarketBooks.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken))
            database.MarketBooks.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year });
        var ordinal = await database.MarketTrades.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).Select(x => (int?)x.Ordinal).MaxAsync(cancellationToken) ?? -1;
        var trade = new MarketTrade { GroupId = group, LeagueId = league, SeasonIndex = year, Id = Guid.NewGuid(), Ordinal = ordinal + 1,
            BuyerTeamId = buyer.Id, BuyerBasketId = buyer.BasketId, SellerTeamId = seller.Id, SellerBasketId = seller.BasketId,
            BuyerOwner = Primary(buyer), SellerOwner = Primary(seller), MoneyFromBuyer = request.MoneyFromBuyer, MoneyFromSeller = request.MoneyFromSeller, CreatedAtUtc = clock.GetUtcNow() };
        foreach (var (fromBuyer, players) in new[] { (true, outgoingBuyer), (false, outgoingSeller) })
            foreach (var (player, index) in players.Select((player, index) => (player, index)))
                trade.Players.Add(new() { FromBuyer = fromBuyer, Ordinal = index, RosterOrdinal = player.Ordinal, PlayerId = player.PlayerId, Player = player.Player,
                    ClubId = player.ClubId, Club = player.Club, PlayerRole = player.PlayerRole, IsActive = player.IsActive, IsVisible = player.IsVisible,
                    Price = player.Price, Revenue = player.Revenue, Position = player.Position, Status = player.Status });
        trade.Votes.Add(new() { TeamId = buyer.Id, BasketId = buyer.BasketId, OwnerEmail = trade.BuyerOwner, ActorUserId = actorId, Approved = true });
        database.MarketTrades.Add(trade);
        Event(trade, actorId, "created");
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "Proposta creata.", Dto(trade));
    }

    public async Task<MarketOperationDto> CommandAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, Guid tradeId, string action,
        MarketCommandRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var (rules, teams, actorId) = await ContextAsync(caller, group, league, year, cancellationToken);
        var trade = await Query(group, league, year).SingleOrDefaultAsync(x => x.Id == tradeId, cancellationToken)
            ?? throw new ApplicationFault(404, "market_not_found", "Proposta non trovata nella competizione richiesta.");
        if (trade.Version != request.ExpectedVersion) throw Changed();
        if (trade.Status != (int)MarketStatus.Pending) throw new ApplicationFault(409, "market_not_pending", "La proposta non è più modificabile.");
        var owned = teams.Where(x => x.Owners.Any(owner => owner.UserId == actorId) && (request.ActorTeamId == null || x.Id == request.ActorTeamId)).ToArray();
        var candidates = owned.Where(x => action is "accept" or "reject" ? x.Id == trade.SellerTeamId : action == "cancel"
            ? x.Id == trade.SellerTeamId || x.Id == trade.BuyerTeamId : x.Id != trade.BuyerTeamId && x.Id != trade.SellerTeamId).ToArray();
        if (candidates.Length == 0) throw Denied();
        if (candidates.Length != 1) throw new ApplicationFault(409, "market_team_selection_required", "Specificare la squadra con cui operare o votare.");
        var team = candidates[0];
        switch (action)
        {
            case "cancel": trade.Status = (int)MarketStatus.Cancelled; break;
            case "reject":
                SetVote(trade, team, actorId, false);
                trade.Status = (int)MarketStatus.Denied;
                break;
            case "accept":
                if (rules.Market == (int)MarketType.Denied) throw Invalid("Il mercato non è abilitato.");
                if (trade.SellerAccepted) throw new ApplicationFault(409, "market_already_accepted", "Proposta già accettata.");
                trade.SellerAccepted = true;
                SetVote(trade, team, actorId, true);
                if (rules.Market == (int)MarketType.WithoutVote || HasQuorum(trade, teams, true)) await ExecuteAsync(trade, rules, teams, cancellationToken);
                break;
            case "approve": case "deny":
                if (rules.Market != (int)MarketType.WithVote || !trade.SellerAccepted) throw Invalid("La proposta deve essere accettata e la lega deve prevedere la votazione.");
                SetVote(trade, team, actorId, action == "approve");
                if (HasQuorum(trade, teams, false)) trade.Status = (int)MarketStatus.Denied;
                else if (HasQuorum(trade, teams, true)) await ExecuteAsync(trade, rules, teams, cancellationToken);
                break;
            default: throw Invalid("Comando di mercato non valido.");
        }
        trade.Version++;
        Event(trade, actorId, action);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var ok = trade.Status is not ((int)MarketStatus.NoMoney) and not ((int)MarketStatus.NoPlayers);
        return new(ok, ok ? "Proposta aggiornata." : trade.Status == (int)MarketStatus.NoMoney ? "Fondi insufficienti: nessuna rosa è stata modificata." : "Giocatori non più disponibili: nessuna rosa è stata modificata.", Dto(trade));
    }

    private async Task ExecuteAsync(MarketTrade trade, LeagueRules rules, FantasyTeamSeason[] teams, CancellationToken cancellationToken)
    {
        if (!new[] { trade.BuyerTeamId, trade.SellerTeamId }.All(id => trade.Votes.Any(vote => vote.TeamId == id && vote.Approved && VoteIsCurrent(vote, teams))))
        { trade.Status = (int)MarketStatus.Expired; return; }
        var buyer = await RosterAsync(trade.GroupId, trade.SeasonIndex, trade.BuyerTeamId, cancellationToken);
        var seller = await RosterAsync(trade.GroupId, trade.SeasonIndex, trade.SellerTeamId, cancellationToken);
        var buyerItems = trade.Players.Where(x => x.FromBuyer).OrderBy(x => x.Ordinal).ToArray();
        var sellerItems = trade.Players.Where(x => !x.FromBuyer).OrderBy(x => x.Ordinal).ToArray();
        var outgoingBuyer = FindPlayers(buyer, buyerItems.Select(x => x.PlayerId).ToArray());
        var outgoingSeller = FindPlayers(seller, sellerItems.Select(x => x.PlayerId).ToArray());
        if (outgoingBuyer == null || outgoingSeller == null || buyerItems.Any(item => !Matches(outgoingBuyer, item)) || sellerItems.Any(item => !Matches(outgoingSeller, item)) ||
            !SameRoles(outgoingBuyer, outgoingSeller)) { trade.Status = (int)MarketStatus.NoPlayers; return; }
        var buyerBudget = (long)rules.StartingMoney - Cost(buyer) + outgoingBuyer.Sum(x => (long)x.Price) - outgoingSeller.Sum(x => (long)x.Price) - trade.MoneyFromBuyer + trade.MoneyFromSeller;
        var sellerBudget = (long)rules.StartingMoney - Cost(seller) + outgoingSeller.Sum(x => (long)x.Price) - outgoingBuyer.Sum(x => (long)x.Price) - trade.MoneyFromSeller + trade.MoneyFromBuyer;
        var buyerMoney = (long)buyer.MarketBalanceAdjustment + trade.MoneyFromSeller - trade.MoneyFromBuyer;
        var sellerMoney = (long)seller.MarketBalanceAdjustment + trade.MoneyFromBuyer - trade.MoneyFromSeller;
        if (buyerBudget < 0 || sellerBudget < 0 || buyerMoney is < int.MinValue or > int.MaxValue || sellerMoney is < int.MinValue or > int.MaxValue)
        { trade.Status = (int)MarketStatus.NoMoney; return; }
        // Remove only the active roster entries. Sold/rebought history is never
        // deleted by a player-name RemoveAll. Immutable trade rows retain the
        // original roster identity, cost and position of the transferred entries.
        var incomingBuyer = outgoingSeller.Select(CopyPlayer).ToArray();
        var incomingSeller = outgoingBuyer.Select(CopyPlayer).ToArray();
        foreach (var role in outgoingBuyer.Select(x => x.PlayerRole).Distinct())
        {
            var buyerRole = outgoingBuyer.Where(x => x.PlayerRole == role).ToArray();
            var sellerRole = outgoingSeller.Where(x => x.PlayerRole == role).ToArray();
            var toBuyer = incomingBuyer.Where(x => x.PlayerRole == role).ToArray();
            var toSeller = incomingSeller.Where(x => x.PlayerRole == role).ToArray();
            for (var i = 0; i < buyerRole.Length; i++) { toBuyer[i].Position = buyerRole[i].Position; toSeller[i].Position = sellerRole[i].Position; }
        }
        var buyerOrdinal = buyer.Players.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1;
        var sellerOrdinal = seller.Players.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1;
        foreach (var player in outgoingBuyer) buyer.Players.Remove(player);
        foreach (var player in outgoingSeller) seller.Players.Remove(player);
        // Flush outgoing unique active-player indexes before adding incoming rows;
        // the transaction remains atomic, including both teams and trade status.
        await database.SaveChangesAsync(cancellationToken);
        foreach (var player in incomingBuyer) { player.Ordinal = buyerOrdinal++; buyer.Players.Add(player); }
        foreach (var player in incomingSeller) { player.Ordinal = sellerOrdinal++; seller.Players.Add(player); }
        buyer.MarketBalanceAdjustment = (int)buyerMoney; seller.MarketBalanceAdjustment = (int)sellerMoney;
        buyer.Version++; seller.Version++;
        buyer.LastUpdateUtc = seller.LastUpdateUtc = clock.GetUtcNow();
        trade.Status = (int)MarketStatus.Approved; trade.ExecutedAtUtc = clock.GetUtcNow();
        await availability.SynchronizeFutureAsync(trade.GroupId, trade.SeasonIndex, [trade.BuyerTeamId, trade.SellerTeamId], cancellationToken);
    }

    private async Task<(LeagueRules Rules, FantasyTeamSeason[] Teams, Guid Actor)> ContextAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var season = await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        if (year != FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime) || season.IsArchived || season.League.IsArchived)
            throw Invalid("Il mercato è disponibile solo per la stagione corrente e per competizioni attive.");
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var actor = await database.Users.Where(x => x.Email == email && x.IsEnabled).Select(x => x.Id).SingleAsync(cancellationToken);
        var teams = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == group && x.SeasonIndex == year && !x.IsArchived && database.LeagueBasketSeasons.Any(p => p.GroupId == group && p.LeagueId == league && p.SeasonIndex == year && p.BasketId == x.BasketId)).AsSplitQuery().ToArrayAsync(cancellationToken);
        var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        return (rules, teams, actor);
    }
    private IQueryable<MarketTrade> Query(Guid group, Guid league, int year) => database.MarketTrades.Include(x => x.Players).ThenInclude(x => x.Player)
        .Include(x => x.Players).ThenInclude(x => x.Club).Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery();
    private async Task<RosterSnapshot> RosterAsync(Guid group, int year, Guid teamId, CancellationToken cancellationToken) =>
        await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .SingleOrDefaultAsync(x => x.GroupId == group && x.SeasonIndex == year && x.TeamId == teamId && x.Kind == RosterKind.Season && x.Day == 0, cancellationToken)
            ?? throw new ApplicationFault(409, "market_roster_unavailable", "Rosa server non disponibile.");
    private static RosterPlayer[]? FindPlayers(RosterSnapshot roster, Guid[]? ids)
    {
        if (ids is null || ids.Length != ids.Distinct().Count()) return null;
        var selected = ids.Select(id => roster.Players.SingleOrDefault(x => x.PlayerId == id && x.Status == 0)).ToArray();
        return selected.Any(x => x == null) ? null : selected.Select(x => x!).ToArray();
    }
    private static bool Matches(RosterPlayer[] rows, MarketTradePlayer item) => rows.Any(x => x.PlayerId == item.PlayerId && x.Price == item.Price && x.PlayerRole == item.PlayerRole && (item.RosterOrdinal == null || x.Ordinal == item.RosterOrdinal));
    private static bool SameRoles(RosterPlayer[] buyer, RosterPlayer[] seller) => buyer.GroupBy(x => x.PlayerRole).Select(x => (x.Key, Count: x.Count())).OrderBy(x => x.Key)
        .SequenceEqual(seller.GroupBy(x => x.PlayerRole).Select(x => (x.Key, Count: x.Count())).OrderBy(x => x.Key));
    private static long Cost(RosterSnapshot row) => row.Players.Sum(x => (long)x.Price) - row.Players.Where(x => x.Status == 3).Sum(x => (long)x.Revenue - x.Price)
        - row.MoneyFromRank - row.MarketBalanceAdjustment - row.OpeningCompetitionPrize - row.Players.Where(x => x.Status == 1).Sum(x => (long)(x.Price / 2));
    private static string Primary(FantasyTeamSeason team) => team.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference;
    private static void SetVote(MarketTrade trade, FantasyTeamSeason team, Guid actor, bool approve)
    {
        var vote = trade.Votes.SingleOrDefault(x => x.TeamId == team.Id);
        if (vote is null) trade.Votes.Add(vote = new() { TeamId = team.Id, BasketId = team.BasketId, OwnerEmail = Primary(team), Ordinal = trade.Votes.Count });
        vote.Approved = approve; vote.ActorUserId = actor;
    }
    private static bool VoteIsCurrent(MarketTradeVote vote, FantasyTeamSeason[] teams) => teams.Any(team => team.Id == vote.TeamId &&
        team.Owners.Any(owner => owner.Membership.User.IsEnabled && owner.Membership.Roles != GroupRole.None &&
            (vote.ActorUserId is { } actor ? owner.UserId == actor : string.Equals(owner.Membership.User.Email, vote.OwnerEmail, StringComparison.OrdinalIgnoreCase))));
    private static bool HasQuorum(MarketTrade trade, FantasyTeamSeason[] teams, bool approve) => trade.Votes.Count(x => x.Approved == approve && VoteIsCurrent(x, teams)) >= teams.Length / 2 + 1;
    private static RosterPlayer CopyPlayer(RosterPlayer source) => new() { PlayerId = source.PlayerId, Player = source.Player, ClubId = source.ClubId, Club = source.Club,
        PlayerRole = source.PlayerRole, IsActive = source.IsActive, IsVisible = source.IsVisible, Price = source.Price, Revenue = source.Revenue, Status = source.Status, Position = source.Position };
    private static MarketDto Dto(MarketTrade trade)
    {
        RosterPlayerDto[] Players(bool buyer) => trade.Players.Where(x => x.FromBuyer == buyer).OrderBy(x => x.Ordinal).Select(x => new RosterPlayerDto(x.PlayerId,
            x.Ordinal, x.Player.Name, new(x.Club.Name, x.Club.Abbreviation), x.PlayerRole, x.IsActive, x.IsVisible, x.Price, x.Revenue, x.Status, x.Position)).ToArray();
        return new(trade.Id, trade.BuyerTeamId, trade.SellerTeamId, trade.BuyerOwner, trade.SellerOwner, Players(true), Players(false), trade.MoneyFromBuyer,
            trade.MoneyFromSeller, trade.Votes.Where(x => x.Approved).OrderBy(x => x.Ordinal).Select(x => x.OwnerEmail).ToArray(),
            trade.Votes.Where(x => !x.Approved).OrderBy(x => x.Ordinal).Select(x => x.OwnerEmail).ToArray(), trade.SellerAccepted, trade.Status, trade.CreatedAtUtc, trade.Version);
    }
    private void Event(MarketTrade trade, Guid actor, string kind) => database.MarketTradeEvents.Add(new()
    {
        GroupId = trade.GroupId, LeagueId = trade.LeagueId, SeasonIndex = trade.SeasonIndex, TradeId = trade.Id, ActorUserId = actor,
        Kind = kind, ResultingStatus = trade.Status, TradeVersion = trade.Version, CreatedAtUtc = clock.GetUtcNow()
    });
    private static ApplicationFault Invalid(string message) => new(400, "invalid_market_request", message);
    private static ApplicationFault Changed() => new(409, "market_changed", "La proposta o la rosa è cambiata. Ricaricare prima di confermare.");
    private static ApplicationFault Denied() => new(403, "market_access_denied", "Non sei proprietario della squadra autorizzata per questa operazione.");
}
