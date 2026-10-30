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
// The jobs only ever run for the current season, so every run also sweeps what
// earlier seasons still hold: nothing produces for them any more.
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
        async Task CloseDayAsync(int season, int day, bool createCheckpoint)
        {
            var checkpoint = await database.PushDayCheckpoints.SingleOrDefaultAsync(x => x.SeasonIndex == season && x.Day == day, cancellationToken);
            if (checkpoint is null && createCheckpoint) database.PushDayCheckpoints.Add(checkpoint = new() { SeasonIndex = season, Day = day });
            if (checkpoint is not null)
            {
                if (checkpoint.ClosedAtUtc is null) { checkpoint.ClosedAtUtc = now; closed++; }
                // The closed flag replaces the baselines as the deduplication guard.
                checkpoint.Votes.Clear(); checkpoint.FormationReceipts.Clear();
                await database.SaveChangesAsync(cancellationToken);
            }
            formationReceipts += await database.PushFormationChangeReceipts.Where(x => x.SeasonIndex == season && x.Day == day).ExecuteDeleteAsync(cancellationToken);
            // Legacy "notification" votes were only the push baseline of that day.
            notificationVotes += await database.FootballVotes.Where(x => x.SeasonIndex == season && x.Day == day && x.Source == FootballVoteSource.Notification).ExecuteDeleteAsync(cancellationToken);
            await database.FootballDatasets.Where(x => x.Kind == "notification" && x.SeasonIndex == season && x.Day == day).ExecuteDeleteAsync(cancellationToken);
            var (removed, scoped) = await DeleteDeliveriesAsync(database.PushDeliveries.Where(x => x.SeasonIndex == season && x.Day == day), now, cutoff, cancellationToken);
            deliveries += removed; confirmations += scoped;
        }
        foreach (var day in concluded) await CloseDayAsync(year, day, createCheckpoint: true);
        // Earlier seasons are over: a day that is still open there (a fantasy result
        // that was never finalised, a delivery past its window) can never be sent
        // again, so it is closed regardless of pending results. Same safety rule for
        // deliveries: nothing under an active lease, nothing still deliverable.
        foreach (var (season, day) in await LeftoverDaysAsync(year, cancellationToken)) await CloseDayAsync(season, day, createCheckpoint: false);
        // Day 0 = market events: deduplicated by MarketTradeEvents.QueuedAtUtc.
        var (market, marketScoped) = await DeleteDeliveriesAsync(database.PushDeliveries.Where(x => x.SeasonIndex <= year && x.Day == 0), now, cutoff, cancellationToken);
        deliveries += market; confirmations += marketScoped;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(closed, deliveries, confirmations, formationReceipts, notificationVotes);
    }

    private async Task<(int Season, int Day)[]> LeftoverDaysAsync(int year, CancellationToken cancellationToken)
    {
        var open = database.PushDayCheckpoints.AsNoTracking().Where(x => x.SeasonIndex < year && (x.ClosedAtUtc == null || x.Votes.Any() || x.FormationReceipts.Any()))
            .Select(x => new { x.SeasonIndex, x.Day });
        var sent = database.PushDeliveries.AsNoTracking().Where(x => x.SeasonIndex < year && x.Day > 0).Select(x => new { x.SeasonIndex, x.Day });
        var receipts = database.PushFormationChangeReceipts.AsNoTracking().Where(x => x.SeasonIndex < year).Select(x => new { x.SeasonIndex, x.Day });
        var baselines = database.FootballVotes.AsNoTracking().Where(x => x.SeasonIndex < year && x.Source == FootballVoteSource.Notification).Select(x => new { x.SeasonIndex, x.Day });
        var days = await open.Concat(sent).Concat(receipts).Concat(baselines).Distinct().OrderBy(x => x.SeasonIndex).ThenBy(x => x.Day).ToArrayAsync(cancellationToken);
        return days.Select(x => (x.SeasonIndex, x.Day)).ToArray();
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
