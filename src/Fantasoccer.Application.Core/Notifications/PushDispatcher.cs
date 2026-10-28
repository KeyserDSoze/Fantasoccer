using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class PushDispatcher(FantasoccerDbContext database, IPushDeliveryTransport transport, IOptions<PushTransportOptions> options, TimeProvider clock)
{
    private const int MaximumAttempts = 10;
    public async Task<PushDispatchResult> DispatchAsync(int maximumDeliveries = 16, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return new(0, 0, 0, 0);
        if (database.ChangeTracker.HasChanges()) throw new InvalidOperationException("Push dispatch requires a dedicated clean scope.");
        maximumDeliveries = Math.Clamp(maximumDeliveries, 1, 32);
        var claimed = 0; var completed = 0; var retried = 0; var suppressed = 0;
        for (var index = 0; index < maximumDeliveries; index++)
        {
            var claim = await ClaimAsync(cancellationToken);
            if (claim is null) break;
            claimed++;
            var (id, lease) = claim.Value;
            var row = await database.PushDeliveries.AsNoTracking().SingleAsync(x => x.Id == id, cancellationToken);
            var status = await CheckAsync(row, cancellationToken);
            var retry = false;
            if (status == PushDeliveryStatus.Pending)
            {
                var email = await database.Users.Where(x => x.Id == row.UserId).Select(x => x.Email).SingleAsync(cancellationToken);
                if (email is null)
                {
                    await FinishAsync(id, lease, PushDeliveryStatus.Suppressed, false, cancellationToken);
                    suppressed++;
                    continue;
                }
                var done = row.Receipts.Select(x => x.EndpointHash).ToArray();
                var targets = await database.PushDeviceBindings.AsNoTracking().Where(x => x.UserId == row.UserId && x.IsActive && x.IsValid && !done.Contains(x.EndpointHash))
                    .OrderBy(x => x.EndpointHash).Take(10).ToArrayAsync(cancellationToken);
                foreach (var target in targets)
                {
                    if (!await database.PushDeliveries.AsNoTracking().AnyAsync(x => x.Id == id && x.LeaseId == lease && x.Status == PushDeliveryStatus.Pending && x.LeaseUntilUtc > clock.GetUtcNow(), cancellationToken))
                        break;
                    // Recheck immediately before every provider request. Worker
                    // recipient binding also suppresses an already-in-flight send
                    // overtaken by logout/account transfer; receipts use CAS.
                    status = await CheckAsync(row, cancellationToken);
                    if (status != PushDeliveryStatus.Pending) break;
                    var current = await database.PushDeviceBindings.AsNoTracking().AnyAsync(x => x.EndpointHash == target.EndpointHash && x.UserId == row.UserId &&
                        x.IsActive && x.IsValid && x.Version == target.Version, cancellationToken);
                    if (!current) continue;
                    var outcome = PushTransportOutcome.Gone;
                    if (PushSubscriptionValidator.IsValid(new(target.Endpoint, target.P256dh, target.Auth)))
                    {
                        var payload = new PushPayload(row.Title, row.Body, new(row.Uri!, email), row.Icon, row.Badge, row.Tag ?? "push-" + row.DeduplicationKey, row.RequireInteraction);
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(15));
                        try { outcome = await transport.SendAsync(new(target.Endpoint, target.P256dh, target.Auth, target.Version), payload,
                            Math.Clamp((int)Math.Ceiling((row.ExpiresAtUtc - clock.GetUtcNow()).TotalSeconds), 1, 86400), timeout.Token); }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch { outcome = PushTransportOutcome.Retry; } // Never log endpoint credentials/provider response bodies.
                    }
                    if (outcome == PushTransportOutcome.Retry) { retry = true; continue; }
                    await RecordAsync(id, lease, target, outcome, cancellationToken);
                }
                if (status == PushDeliveryStatus.Pending && !retry)
                {
                    var receipts = await database.PushDeliveries.AsNoTracking().Where(x => x.Id == id).SelectMany(x => x.Receipts).Select(x => x.EndpointHash).ToArrayAsync(cancellationToken);
                    retry = await database.PushDeviceBindings.AnyAsync(x => x.UserId == row.UserId && x.IsActive && x.IsValid && !receipts.Contains(x.EndpointHash), cancellationToken);
                }
            }
            await FinishAsync(id, lease, status, retry, cancellationToken);
            if (status != PushDeliveryStatus.Pending) suppressed++;
            else if (retry) retried++;
            else completed++;
        }
        return new(claimed, completed, retried, suppressed);
    }
    private async Task<(Guid, Guid)?> ClaimAsync(CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var now = clock.GetUtcNow();
        var row = await database.PushDeliveries.Where(x => x.Status == PushDeliveryStatus.Pending && x.NextAttemptAtUtc <= now && (x.LeaseUntilUtc == null || x.LeaseUntilUtc < now))
            .OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (row is null) { database.ChangeTracker.Clear(); return null; }
        var lease = Guid.NewGuid(); row.LeaseId = lease; row.LeaseUntilUtc = now.AddMinutes(5);
        row.Attempts++;
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return (row.Id, lease);
    }
    private async Task<PushDeliveryStatus> CheckAsync(PushDelivery row, CancellationToken cancellationToken)
    {
        if (row.ExpiresAtUtc <= clock.GetUtcNow()) return PushDeliveryStatus.Expired;
        if (row.Attempts > MaximumAttempts) return PushDeliveryStatus.AttemptsExhausted;
        if (!PushOutboxApplicationService.SafeUri(row.Uri) || row.GroupId == null || row.LeagueId == null) return PushDeliveryStatus.Quarantined;
        var preferences = await database.UserPushPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == row.UserId, cancellationToken);
        return !PushDeliveryAccess.IsEnabled(preferences, row.Kind) || !await PushDeliveryAccess.IsCurrentOwnerAsync(database, row, cancellationToken)
            ? PushDeliveryStatus.Suppressed : PushDeliveryStatus.Pending;
    }
    private async Task RecordAsync(Guid id, Guid lease, PushDeviceBinding target, PushTransportOutcome outcome, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var row = await database.PushDeliveries.SingleAsync(x => x.Id == id, cancellationToken);
        if (row.LeaseId != lease) return;
        if (!row.Receipts.Any(x => x.EndpointHash == target.EndpointHash)) row.Receipts.Add(new() { Ordinal = row.Receipts.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1,
            EndpointHash = target.EndpointHash, BindingVersion = target.Version, Outcome = PushOutcomes.Stored(outcome), RecordedAtUtc = clock.GetUtcNow() });
        if (outcome == PushTransportOutcome.Gone)
        {
            var binding = await database.PushDeviceBindings.SingleOrDefaultAsync(x => x.EndpointHash == target.EndpointHash && x.UserId == target.UserId && x.Version == target.Version, cancellationToken);
            if (binding != null) { binding.IsActive = false; binding.IsValid = false; binding.P256dh = ""; binding.Auth = ""; binding.DeviceProofHash = null; binding.Version++; }
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }
    private async Task FinishAsync(Guid id, Guid lease, PushDeliveryStatus status, bool retry, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var row = await database.PushDeliveries.SingleAsync(x => x.Id == id, cancellationToken);
        if (row.LeaseId != lease) return;
        row.LeaseId = null; row.LeaseUntilUtc = null;
        if (status != PushDeliveryStatus.Pending) row.Status = status;
        else if (retry && row.Attempts >= MaximumAttempts) row.Status = PushDeliveryStatus.AttemptsExhausted;
        else if (!retry) row.Status = PushDeliveryStatus.Completed;
        else row.NextAttemptAtUtc = clock.GetUtcNow().AddMinutes(Math.Min(15, Math.Pow(2, Math.Min(row.Attempts - 1, 4))));
        if (row.Status != PushDeliveryStatus.Pending) row.CompletedAtUtc = clock.GetUtcNow();
        if (row.Status == PushDeliveryStatus.Quarantined) row.QuarantineReason = "scope_or_link_invalid";
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }
}
