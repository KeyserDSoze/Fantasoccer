using System.Security.Claims;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Auctions;

public sealed partial class AuctionApplicationService
{
    public async Task<AuctionUpdateDto> CommandAsync(ClaimsPrincipal caller, Guid id, string operation, AuctionCommandRequest request, CancellationToken cancellationToken = default)
    {
        AuctionUpdateDto update;
        await using (var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken))
        {
            var auction = await SessionAsync(id, true, cancellationToken);
            var actor = await ActorAsync(caller, auction.GroupId, cancellationToken);
            var admin = IsAuctioneer(actor.Roles);
            if (!admin && operation != "bid") throw Denied();
            if (auction.Version != request.ExpectedVersion) throw new ApplicationFault(409, "auction_changed", "L'asta è cambiata. Ricaricare lo stato prima di riprovare.");
            if (auction.Status == 3 && operation != "reopen") throw new ApplicationFault(409, "auction_finished", "L'asta è terminata.");
            if (auction.SeasonIndex != FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime)) throw Invalid("L'asta non appartiene alla stagione corrente.");
            var teams = await TeamsAsync(auction.GroupId, auction.LeagueId, auction.SeasonIndex, cancellationToken);
            var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == auction.GroupId && x.LeagueId == auction.LeagueId && x.SeasonIndex == auction.SeasonIndex, cancellationToken);
            switch (operation)
            {
                case "role":
                    if (request.Role is not (>= 0 and <= 3)) throw Invalid("Ruolo non valido.");
                    auction.CurrentRole = request.Role.Value; break;
                case "timer":
                    if (request.Timer is not (>= 1 and <= 600)) throw Invalid("Il timer deve essere fra 1 e 600 secondi.");
                    auction.SecondsPerAuction = request.Timer.Value; break;
                case "pause": auction.Status = 1; break;
                case "resume": auction.Status = 2; break;
                case "close": auction.Status = 3; break;
                case "reopen": auction.Status = 1; break;
                case "close-current": auction.Actual = null; auction.Status = 1; break;
                case "reset-current":
                    if (auction.Actual is null) throw Invalid("Nessun giocatore è in asta.");
                    auction.Actual.WinningTeamId = null; auction.Actual.WinningBasketId = null; auction.Actual.OwnerEmail = null;
                    auction.Actual.Price = 0; auction.Actual.SubstitutedPlayerId = null; auction.Actual.TimerStartedAtUtc = null;
                    break;
                case "reset-timer":
                    if (auction.Actual is null) throw Invalid("Nessun giocatore è in asta.");
                    auction.Actual.TimerStartedAtUtc = clock.GetUtcNow(); break;
                case "transfer-auctioneer":
                    var target = await database.GroupMemberships.Include(x => x.User).SingleOrDefaultAsync(x => x.GroupId == auction.GroupId && x.UserId == request.NewAuctioneerUserId && x.User.IsEnabled, cancellationToken);
                    if (target is null || !IsAuctioneer(target.Roles)) throw Invalid("Il nuovo banditore deve essere un amministratore attivo del gruppo.");
                    if (target.User.Email is null) throw Invalid("Il banditore deve avere un'email di accesso configurata.");
                    auction.CreatorUserId = target.UserId; auction.CreatorEmail = target.User.Email; break;
                case "show": await ShowAsync(caller, auction, request, teams, cancellationToken); break;
                case "bid": await BidAsync(auction, request, teams, actor.Id, admin, rules, cancellationToken); break;
                case "assign": await AssignAsync(auction, teams, rules, cancellationToken); break;
                case "player-status": await ChangeStatusAsync(auction, request, teams, actor.Id, rules, cancellationToken); break;
                case "finalize": await FinalizeAsync(auction, teams, cancellationToken); break;
                default: throw Invalid("Comando asta non valido.");
            }
            auction.Version++;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            update = new(auction.Id, auction.Version, operation);
        }
        // Every transport uses this exact service. A refresh event carries no
        // caller-specific permissions, bids, future player order or roster data.
        await publisher.PublishAsync(update, cancellationToken);
        return update;
    }

    private async Task ShowAsync(ClaimsPrincipal caller, AuctionSession auction, AuctionCommandRequest request, FantasyTeamSeason[] teams, CancellationToken cancellationToken)
    {
        if (request.Role is not (>= 0 and <= 3)) throw Invalid("Ruolo non valido.");
        if (auction.Actual?.WinningTeamId != null) throw new ApplicationFault(409, "current_lot_has_bid", "Assegnare o chiudere il lotto attuale prima di mostrare un altro giocatore.");
        var next = request.PlayerId ?? auction.PlayerOrder.Where(x => x.Role == request.Role && !x.IsShown).OrderBy(x => x.Ordinal).Select(x => x.PlayerId).FirstOrDefault();
        if (next is null) throw new ApplicationFault(409, "auction_role_exhausted", "Non ci sono altri giocatori per questo ruolo.");
        if (auction.Winnings.Any(x => x.PlayerId == next) || await IsOwnedInLeagueAsync(auction, teams, next, cancellationToken)) throw Invalid("Il giocatore è già assegnato a una squadra.");
        var player = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .SingleOrDefaultAsync(x => x.PlayerId == next && x.SeasonIndex == auction.SeasonIndex && (int)x.Position == request.Role, cancellationToken)
            ?? throw Invalid("Giocatore non presente nel catalogo del ruolo richiesto.");
        if (player.Club is null) throw Invalid("Squadra reale non disponibile.");
        var stat = (await football.StatisticsAsync(caller, auction.SeasonIndex, cancellationToken))?.Players.SingleOrDefault(x => x.PlayerId == next);
        var shown = auction.PlayerOrder.FirstOrDefault(x => x.PlayerId == next && x.Role == request.Role);
        if (shown is not null) shown.IsShown = true;
        var last = auction.LastShown.SingleOrDefault(x => x.Role == request.Role);
        if (last is null) auction.LastShown.Add(last = new() { Role = request.Role.Value });
        last.PlayerId = next;
        auction.Actual = new() { PlayerId = player.PlayerId, ClubId = player.ClubId!, PlayerRole = (int)player.Position,
            IsActive = player.IsActive, IsVisible = player.IsVisible, StartedAtUtc = clock.GetUtcNow(), Statistics = Snapshot(stat) };
        auction.CurrentRole = request.Role.Value;
        auction.Status = 2;
    }

    private async Task BidAsync(AuctionSession auction, AuctionCommandRequest request, FantasyTeamSeason[] teams, Guid actor, bool admin, LeagueRules rules, CancellationToken cancellationToken)
    {
        if (auction.Status == 1 && !admin) throw Invalid("L'asta è in pausa.");
        var lot = auction.Actual ?? throw Invalid("Nessun giocatore è in asta.");
        if (lot.TimerStartedAtUtc is { } timer && clock.GetUtcNow() > timer.AddSeconds(auction.SecondsPerAuction + 2))
            throw new ApplicationFault(409, "auction_timer_expired", "Il tempo per le offerte è scaduto.");
        var eligible = teams.Where(x => request.TeamId != null ? x.Id == request.TeamId : x.Owners.Any(owner => owner.UserId == actor)).ToArray();
        if (eligible.Length != 1) throw Invalid("Specificare una sola squadra per l'offerta.");
        var team = eligible[0];
        if (!admin && !team.Owners.Any(owner => owner.UserId == actor)) throw Denied();
        if (lot.WinningTeamId == team.Id) throw Invalid("La squadra è già il miglior offerente.");
        if (request.Amount is not (> 0) || request.Amount <= lot.Price) throw Invalid("L'offerta deve essere maggiore dell'offerta attuale.");
        var roster = await RosterAsync(auction, team.Id, cancellationToken);
        ValidateBid(auction, lot, roster, rules, request.Amount.Value, request.SubstitutedPlayerId,
            await SubstitutionRefundAsync(auction, roster, rules, request.SubstitutedPlayerId, cancellationToken));
        lot.WinningTeamId = team.Id; lot.WinningBasketId = team.BasketId; lot.OwnerEmail = team.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference;
        lot.Price = request.Amount.Value; lot.SubstitutedPlayerId = request.SubstitutedPlayerId; lot.TimerStartedAtUtc = clock.GetUtcNow();
    }

    private async Task AssignAsync(AuctionSession auction, FantasyTeamSeason[] teams, LeagueRules rules, CancellationToken cancellationToken)
    {
        var lot = auction.Actual ?? throw Invalid("Nessun giocatore è in asta.");
        if (lot.WinningTeamId is not { } teamId || !teams.Any(x => x.Id == teamId)) throw Invalid("Nessuna squadra ha un'offerta valida.");
        if (auction.Winnings.Any(x => x.PlayerId == lot.PlayerId) || await IsOwnedInLeagueAsync(auction, teams, lot.PlayerId, cancellationToken)) throw Invalid("Il giocatore è già assegnato.");
        var roster = await RosterAsync(auction, teamId, cancellationToken);
        var refund = await SubstitutionRefundAsync(auction, roster, rules, lot.SubstitutedPlayerId, cancellationToken);
        ValidateBid(auction, lot, roster, rules, lot.Price, lot.SubstitutedPlayerId, refund);
        if (lot.SubstitutedPlayerId is { } replaced)
        {
            // A player who left Serie A gives back the share fixed by the league rules; any other replaced player is discarded with no refund.
            var row = roster.Players.Single(x => x.PlayerId == replaced && x.Status == 0);
            if (refund is { } credits) { row.Status = RosterEconomy.LeftSerieA; row.Revenue = credits; }
            else row.Status = (int)PlayerInTeamStatus.SoldWithNoReturnedPrice;
        }
        roster.Players.Add(new() { Ordinal = roster.Players.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1, PlayerId = lot.PlayerId,
            ClubId = lot.ClubId, PlayerRole = lot.PlayerRole, IsActive = lot.IsActive, IsVisible = lot.IsVisible, Price = lot.Price, Status = 0, Position = (int)FantaSoccerRole.Tribune });
        roster.Version++; roster.LastUpdateUtc = clock.GetUtcNow();
        auction.Winnings.Add(new() { Ordinal = auction.Winnings.Count, PlayerId = lot.PlayerId, TeamId = teamId, BasketId = roster.BasketId, OwnerEmail = lot.OwnerEmail, Price = lot.Price });
        auction.Actual = null;
        await availability.SynchronizeFutureAsync(auction.GroupId, auction.SeasonIndex, [teamId], cancellationToken);
    }

    private async Task ChangeStatusAsync(AuctionSession auction, AuctionCommandRequest request, FantasyTeamSeason[] teams, Guid actor, LeagueRules rules, CancellationToken cancellationToken)
    {
        if (request.TeamId is not { } teamId || !teams.Any(x => x.Id == teamId) || request.PlayerStatus is not (>= 0 and <= 5) || request.RosterOrdinal is null) throw Invalid("Contesto giocatore non valido.");
        var roster = await RosterAsync(auction, teamId, cancellationToken);
        if (request.ExpectedRosterVersion != roster.Version) throw new ApplicationFault(409, "roster_changed", "Ricaricare la rosa prima di modificarla.");
        var player = roster.Players.SingleOrDefault(x => x.Ordinal == request.RosterOrdinal && x.PlayerId == request.PlayerId) ?? throw Invalid("Giocatore non trovato nella riga richiesta.");
        if (request.PlayerStatus == 0 && roster.Players.Any(x => x.Ordinal != player.Ordinal && x.PlayerId == player.PlayerId && x.Status == 0)) throw Invalid("Esiste già una riga attiva per il giocatore.");
        if (request.PlayerStatus == 0 && player.Status != 0 && await IsOwnedInLeagueAsync(auction, teams, player.PlayerId, cancellationToken)) throw Invalid("Il giocatore appartiene già a un'altra rosa della lega.");
        if (request.PlayerStatus == 0 && player.Status != 0 && (roster.Players.Count(x => x.Status == 0) >= 25 ||
            player.PlayerRole is < 0 or > 3 || roster.Players.Count(x => x.Status == 0 && x.PlayerRole == player.PlayerRole) >= new[] { 3, 8, 8, 6 }[player.PlayerRole]))
            throw Invalid("Il reintegro supererebbe i limiti della rosa o del ruolo.");
        if (request.PlayerStatus == 3 && request.Revenue is not (>= 0)) throw Invalid("Specificare il prezzo di vendita (zero o più crediti).");
        database.AuctionRosterChanges.Add(new() { GroupId = auction.GroupId, LeagueId = auction.LeagueId, SeasonIndex = auction.SeasonIndex, AuctionId = auction.Id,
            ActorUserId = actor, TeamId = roster.TeamId, BasketId = roster.BasketId, PlayerId = player.PlayerId, RosterOrdinal = player.Ordinal, PreviousStatus = player.Status,
            Status = request.PlayerStatus!.Value, Price = player.Price, Revenue = player.Revenue, ChangedAtUtc = clock.GetUtcNow() });
        if (request.PlayerStatus == 4)
        {
            roster.Players.Remove(player);
        }
        else
        {
            // Sale: the whole sale price comes back. Left Serie A / injured: the share of the price fixed by the league rules now.
            player.Status = request.PlayerStatus.Value;
            player.Revenue = request.PlayerStatus switch
            {
                3 => request.Revenue!.Value,
                RosterEconomy.LeftSerieA => RosterEconomy.LeftSerieARefund(player.Price, rules.LeftSerieARefundPercent, rules.LeftSerieARefundRoundUp),
                _ => 0,
            };
        }
        roster.Version++; roster.LastUpdateUtc = clock.GetUtcNow();
        await availability.SynchronizeFutureAsync(auction.GroupId, auction.SeasonIndex, [teamId], cancellationToken);
    }

    private async Task FinalizeAsync(AuctionSession auction, FantasyTeamSeason[] teams, CancellationToken cancellationToken)
    {
        if (auction.Actual?.WinningTeamId != null) throw Invalid("Assegnare o chiudere prima il lotto con offerta.");
        var future = await database.FootballMatches.Where(x => x.SeasonIndex == auction.SeasonIndex && (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc) != null)
            .GroupBy(x => x.SerieADay).Select(x => new { Day = x.Key, Start = x.Min(game => game.WasPostponed ? game.OriginalKickoffUtc : game.KickoffUtc) }).Where(x => x.Start > clock.GetUtcNow())
            .OrderBy(x => x.Start).FirstOrDefaultAsync(cancellationToken);
        if (future is not null)
        {
            var started = await database.FootballMatches.AnyAsync(x => x.SeasonIndex == auction.SeasonIndex && x.SerieADay == future.Day && (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc) <= clock.GetUtcNow(), cancellationToken);
            if (!started)
                foreach (var team in teams)
                {
                    var source = await RosterAsync(auction, team.Id, cancellationToken);
                    // Never overwrite an existing submitted formation or a live day.
                    if (!await database.RosterSnapshots.AnyAsync(x => x.GroupId == auction.GroupId && x.SeasonIndex == auction.SeasonIndex && x.TeamId == team.Id && x.Kind == RosterKind.Matchday && x.Day == future.Day, cancellationToken))
                        database.RosterSnapshots.Add(TeamRosterApplicationService.Copy(source, RosterKind.Matchday, future.Day, null));
                }
        }
        auction.Actual = null; auction.Status = 3;
    }

    private async Task<RosterSnapshot> RosterAsync(AuctionSession auction, Guid teamId, CancellationToken cancellationToken) =>
        await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Include(x => x.AdditionalOwners).ThenInclude(x => x.User).Where(x => x.GroupId == auction.GroupId && x.SeasonIndex == auction.SeasonIndex && x.TeamId == teamId && x.Kind == RosterKind.Season)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken) ?? throw new ApplicationFault(409, "auction_roster_unavailable", "Rosa non disponibile.");
    private Task<bool> IsOwnedInLeagueAsync(AuctionSession auction, FantasyTeamSeason[] teams, string player, CancellationToken cancellationToken)
    {
        var ids = teams.Select(x => x.Id).ToArray();
        return database.RosterSnapshots.Where(x => x.GroupId == auction.GroupId && x.SeasonIndex == auction.SeasonIndex && x.Kind == RosterKind.Season && ids.Contains(x.TeamId))
            .SelectMany(x => x.Players).AnyAsync(x => x.PlayerId == player && x.Status == 0, cancellationToken);
    }
    // Refund for the replaced player when he left Serie A (null: an ordinary discard, nothing comes back).
    private async Task<int?> SubstitutionRefundAsync(AuctionSession auction, RosterSnapshot roster, LeagueRules rules, string? replaced, CancellationToken cancellationToken)
    {
        if (replaced is null || roster.Players.SingleOrDefault(x => x.PlayerId == replaced && x.Status == 0) is not { } row) return null;
        var left = await database.PlayerSeasons.AnyAsync(x => x.PlayerId == replaced && x.SeasonIndex == auction.SeasonIndex && x.ClubId == null && x.PreviousClubId != null, cancellationToken);
        return left ? RosterEconomy.LeftSerieARefund(row.Price, rules.LeftSerieARefundPercent, rules.LeftSerieARefundRoundUp) : null;
    }

    private static void ValidateBid(AuctionSession auction, AuctionCurrentLot lot, RosterSnapshot roster, LeagueRules rules, int price, string? replaced, int? refund)
    {
        var active = roster.Players.Where(x => x.Status == 0).ToArray();
        var substitute = replaced is null ? null : active.SingleOrDefault(x => x.PlayerId == replaced) ?? throw Invalid("Il sostituito non è attivo nella rosa della squadra.");
        if (substitute != null && (auction.Kind != 1 || substitute.PlayerRole != lot.PlayerRole)) throw Invalid("La sostituzione richiede un'asta di riparazione e lo stesso ruolo.");
        var limits = new[] { 3, 8, 8, 6 };
        if (lot.PlayerRole is < 0 or > 3 || active.Count(x => x.PlayerRole == lot.PlayerRole) + 1 - (substitute == null ? 0 : 1) > limits[lot.PlayerRole]) throw Invalid("Limite di giocatori per ruolo raggiunto.");
        var finalCount = active.Length + 1 - (substitute == null ? 0 : 1);
        if (finalCount > 25) throw Invalid("La rosa non può superare 25 giocatori.");
        var cost = RosterEconomy.Cost(roster) - (refund ?? 0);
        if (price < 1 || (long)rules.StartingMoney - cost - price - Math.Max(0, 25 - finalCount) < 0) throw Invalid("Budget insufficiente, inclusi i crediti per completare la rosa.");
    }
    private static AuctionPlayerStatistic Snapshot(StatisticPlayerDto? row) => row is null ? new() : new()
    {
        Summatory = row.Summatory, FantaSummatory = row.FantaSummatory, WithVote = row.WithVote, WithoutVote = row.WithoutVote, NoPlayed = row.NoPlayed,
        WithSpecial = row.WithSpecial, Goals = row.Goals, Penalties = row.Penalties, Assists = row.Assists, StoppedPenalties = row.StoppedPenalties,
        SufferedGoals = row.SufferedGoals, WrongedPenalties = row.WrongedPenalties, OwnGoals = row.OwnGoals, YellowCards = row.YellowCards, RedCards = row.RedCards,
        EnoughVotes = row.EnoughVotes, ManOfTheMatch = row.ManOfTheMatch, Injured = row.Injured, HasDescription = row.Description != null,
        DescriptionTop = row.Description?.Top, DescriptionDown = row.Description?.Down, HasGameHistory = true,
        Games = row.Games.Select((game, index) => new FootballStatisticGame { Ordinal = index, Day = game.SerieADay, Vote = game.Vote, Positiveness = game.Positiveness }).ToArray()
    };
}
