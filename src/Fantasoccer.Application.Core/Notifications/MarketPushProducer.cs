using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class MarketPushProducer(FantasoccerDbContext database, PushOutboxApplicationService outbox, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (database.ChangeTracker.HasChanges()) throw new InvalidOperationException("Market push production requires a dedicated clean scope.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var events = await database.MarketTradeEvents.Where(x => x.QueuedAtUtc == null).OrderBy(x => x.CreatedAtUtc).Take(100).ToArrayAsync(cancellationToken);
        if (events.Length == 0) { database.ChangeTracker.Clear(); return; }
        foreach (var item in events)
        {
            var trade = await database.MarketTrades.SingleAsync(x => x.GroupId == item.GroupId && x.LeagueId == item.LeagueId && x.SeasonIndex == item.SeasonIndex && x.Id == item.TradeId, cancellationToken);
            var league = await database.LeagueSeasons.Include(x => x.League).SingleAsync(x => x.GroupId == item.GroupId && x.LeagueId == item.LeagueId && x.SeasonIndex == item.SeasonIndex, cancellationToken);
            if (league.League.IsMain && !league.IsArchived && !league.League.IsArchived)
            {
                var recipients = await database.LeagueParticipantOwners.Where(x => x.GroupId == item.GroupId && x.LeagueId == item.LeagueId && x.SeasonIndex == item.SeasonIndex &&
                    x.UserId != item.ActorUserId && (item.Kind != "created" || x.TeamId == trade.SellerTeamId) &&
                    database.GroupMemberships.Any(m => m.GroupId == x.GroupId && m.UserId == x.UserId && m.Roles != GroupRole.None && m.User.IsEnabled))
                    .Select(x => x.UserId).Distinct().ToArrayAsync(cancellationToken);
                foreach (var user in recipients)
                {
                    var preferences = await database.UserPushPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user, cancellationToken);
                    if (!PushDeliveryAccess.IsEnabled(preferences, 4)) continue;
                    var body = item.ResultingStatus switch
                    {
                        1 => "Uno scambio è stato completato.", 2 => "Una proposta è stata rifiutata.", 3 => "Una proposta è stata annullata.",
                        4 => "Scambio non completato: fondi insufficienti.", 5 => "Scambio non completato: giocatori non più disponibili.",
                        6 => "Una proposta di scambio è scaduta.", _ => item.Kind == "created" ? "Hai ricevuto una proposta di scambio." : item.Kind == "accept" ? "Una proposta è stata accettata. Votazione aperta." : "La votazione di una proposta è stata aggiornata."
                    };
                    await outbox.EnqueueAsync(new($"market:{item.Id}", item.GroupId, item.LeagueId, item.SeasonIndex, 0, user, 4, "FantaZone",
                        $"[{league.League.Name}] {body}", $"/market/trades?groupId={item.GroupId}&leagueId={item.LeagueId}&year={item.SeasonIndex}", item.CreatedAtUtc.AddHours(48)), cancellationToken);
                }
            }
            item.QueuedAtUtc = clock.GetUtcNow();
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}
