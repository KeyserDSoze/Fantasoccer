using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class PushRetentionOptions
{
    // Safety window after the effective end of a matchday (recoveries included)
    // and after the last retry/expiry of a delivery.
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(48);
}

public sealed record PushRetentionResult(int ClosedDays, int Deliveries, int ScopeConfirmations, int FormationReceipts, int NotificationVotes);

// Keeps the outbox transactional but not an archive. A concluded day is first
// CLOSED (the checkpoint row stays, so producers never emit it again), then its
// delivery details, receipts and dedup baselines are removed. Preferences,
// device bindings, game data and anything still pending or leased are untouched.
public sealed class PushRetentionService(FantasoccerDbContext database, IOptions<PushRetentionOptions> options, TimeProvider clock)
{
    public async Task<PushRetentionResult> CleanupAsync(int year, CancellationToken cancellationToken = default)
    {
        if (database.ChangeTracker.HasChanges()) throw new InvalidOperationException("Push retention requires a dedicated clean scope.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var now = clock.GetUtcNow();
        var cutoff = now - options.Value.Retention;
        var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        // Days still waiting for a recovery or a pending fantasy result keep everything.
        var pendingResults = (await database.FantasyMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.Result == null &&
                database.LeagueSeasons.Any(s => s.GroupId == x.GroupId && s.LeagueId == x.LeagueId && s.SeasonIndex == year && !s.IsArchived && !s.League.IsArchived))
            .Select(x => x.Matchday.SerieADay).Distinct().ToArrayAsync(cancellationToken)).ToHashSet();
        var concluded = matches.GroupBy(x => x.SerieADay).Where(day => !pendingResults.Contains(day.Key) && day.All(game => !game.Delayed && game.KickoffUtc is not null &&
                (game.IsFinished == true || now >= game.KickoffUtc.Value.AddMinutes(135))) && day.Max(game => game.KickoffUtc!.Value).AddMinutes(135) <= cutoff)
            .Select(x => x.Key).Order().ToArray();
        var closed = 0; var deliveries = 0; var confirmations = 0; var formationReceipts = 0; var notificationVotes = 0;
        foreach (var day in concluded)
        {
            var checkpoint = await database.PushDayCheckpoints.SingleOrDefaultAsync(x => x.SeasonIndex == year && x.Day == day, cancellationToken);
            if (checkpoint is null) database.PushDayCheckpoints.Add(checkpoint = new() { SeasonIndex = year, Day = day });
            if (checkpoint.ClosedAtUtc is null) { checkpoint.ClosedAtUtc = now; closed++; }
            // The closed flag replaces the baselines as the deduplication guard.
            checkpoint.Votes.Clear(); checkpoint.FormationReceipts.Clear();
            await database.SaveChangesAsync(cancellationToken);
            formationReceipts += await database.PushFormationChangeReceipts.Where(x => x.SeasonIndex == year && x.Day == day).ExecuteDeleteAsync(cancellationToken);
            // Legacy "notification" votes were only the push baseline of that day.
            notificationVotes += await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day && x.Source == FootballVoteSource.Notification).ExecuteDeleteAsync(cancellationToken);
            await database.FootballDatasets.Where(x => x.Kind == "notification" && x.SeasonIndex == year && x.Day == day).ExecuteDeleteAsync(cancellationToken);
            var (removed, scoped) = await DeleteDeliveriesAsync(database.PushDeliveries.Where(x => x.SeasonIndex == year && x.Day == day), now, cutoff, cancellationToken);
            deliveries += removed; confirmations += scoped;
        }
        // Day 0 = market events: deduplicated by MarketTradeEvents.QueuedAtUtc.
        var (market, marketScoped) = await DeleteDeliveriesAsync(database.PushDeliveries.Where(x => x.SeasonIndex <= year && x.Day == 0), now, cutoff, cancellationToken);
        deliveries += market; confirmations += marketScoped;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(closed, deliveries, confirmations, formationReceipts, notificationVotes);
    }

    private async Task<(int Deliveries, int Confirmations)> DeleteDeliveriesAsync(IQueryable<PushDelivery> scope, DateTimeOffset now, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // Never under an active lease; pending/quarantined rows only once they can
        // no longer be sent (expired beyond the safety window).
        var removable = scope.Where(x => (x.LeaseUntilUtc == null || x.LeaseUntilUtc < now) &&
            ((x.Status != PushDeliveryStatus.Pending && x.Status != PushDeliveryStatus.Quarantined && (x.CompletedAtUtc ?? x.ExpiresAtUtc) < cutoff) ||
             x.ExpiresAtUtc < cutoff));
        // Same predicate for both statements; the global write lock held by this
        // transaction keeps dispatchers from claiming rows in between.
        var confirmations = await database.PushDeliveryScopeConfirmations.Where(x => removable.Select(d => d.Id).Contains(x.DeliveryId)).ExecuteDeleteAsync(cancellationToken);
        var deliveries = await removable.ExecuteDeleteAsync(cancellationToken);
        return (deliveries, confirmations);
    }
}
