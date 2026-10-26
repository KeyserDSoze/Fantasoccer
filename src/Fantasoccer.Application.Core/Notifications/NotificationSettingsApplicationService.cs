using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class NotificationSettingsApplicationService(FantasoccerDbContext database, AccountApplicationService accounts,
    IOptions<PushTransportOptions> options, TimeProvider clock)
{
    public async Task<PushSettingsDto> GetAsync(ClaimsPrincipal caller, string? email = null, CancellationToken cancellationToken = default)
    {
        var actor = await ActorAsync(caller, email, cancellationToken);
        return await ProjectAsync(actor.Id, cancellationToken);
    }
    public async Task<PushSettingsDto> PatchAsync(ClaimsPrincipal caller, PushPreferencesPatch request, string? email = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await ActorAsync(caller, email, cancellationToken);
        var preferences = await PreferencesAsync(actor.Id, cancellationToken);
        preferences.MatchEvents = request.MatchEvents ?? preferences.MatchEvents;
        preferences.OpponentMatchEvents = request.OpponentMatchEvents ?? preferences.OpponentMatchEvents;
        preferences.DeploymentReminder = request.DeploymentReminder ?? preferences.DeploymentReminder;
        preferences.EndDay = request.EndDay ?? preferences.EndDay;
        preferences.MarketEvents = request.MarketEvents ?? preferences.MarketEvents;
        preferences.LiveChangesReminder = request.LiveChangesReminder ?? preferences.LiveChangesReminder;
        preferences.LiveFormationChangeEvents = request.LiveFormationChangeEvents ?? preferences.LiveFormationChangeEvents;
        preferences.Version++;
        await database.SaveChangesAsync(cancellationToken);
        var result = await ProjectAsync(actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    public async Task<RegisteredNotificationDeviceDto> RegisterAsync(ClaimsPrincipal caller, RegisterNotificationDeviceRequest request, string? email = null, CancellationToken cancellationToken = default)
    {
        var subscription = request.Subscription;
        if (subscription is null || request.PreviousEndpoint?.Length > 4096 || request.DeviceProof?.Length > 128 ||
            !PushSubscriptionValidator.IsValid(new(subscription.Endpoint, subscription.P256dh, subscription.Auth)))
            throw new ApplicationFault(400, "invalid_push_subscription", "Subscription push non valida.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await ActorAsync(caller, email, cancellationToken);
        await PreferencesAsync(actor.Id, cancellationToken); // Retain explicit opt-outs; registration never enables preference flags.
        var hash = PushEndpointIdentity.Hash(subscription.Endpoint);
        var binding = await database.PushDeviceBindings.SingleOrDefaultAsync(x => x.EndpointHash == hash, cancellationToken);
        Guid? previous = binding?.UserId;
        if (binding is { IsActive: true } && binding.UserId != actor.Id && !MatchesProof(binding.DeviceProofHash, request.DeviceProof))
            throw new ApplicationFault(409, "device_reactivation_required", "Il dispositivo appartiene a un'altra sessione: disattivare la vecchia subscription e riattivare dal browser corrente. Nessuna impostazione altrui è stata restituita o modificata.");
        if (binding is null) database.PushDeviceBindings.Add(binding = new() { EndpointHash = hash, UserId = actor.Id, Endpoint = subscription.Endpoint, P256dh = subscription.P256dh, Auth = subscription.Auth });
        else binding.Version++;
        if (!string.IsNullOrEmpty(request.PreviousEndpoint))
        {
            var previousHash = PushEndpointIdentity.Hash(request.PreviousEndpoint);
            var old = previousHash == hash ? null : await database.PushDeviceBindings.SingleOrDefaultAsync(x => x.EndpointHash == previousHash && x.UserId == actor.Id, cancellationToken);
            if (old != null) Deactivate(old);
        }
        var proof = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        binding.UserId = actor.Id; binding.Endpoint = subscription.Endpoint; binding.P256dh = subscription.P256dh; binding.Auth = subscription.Auth;
        binding.IsValid = true; binding.IsActive = true; binding.DeviceProofHash = ProofHash(proof);
        database.PushDeviceOwnershipEvents.Add(new() { EndpointHash = hash, PreviousUserId = previous, UserId = actor.Id, BindingVersion = binding.Version, RecordedAtUtc = clock.GetUtcNow() });
        await database.SaveChangesAsync(cancellationToken);
        var settings = await ProjectAsync(actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(settings, proof);
    }
    public async Task<PushSettingsDto> RemoveAsync(ClaimsPrincipal caller, string endpoint, string? email = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 4096) throw new ApplicationFault(400, "invalid_push_endpoint", "Endpoint non valido.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await ActorAsync(caller, email, cancellationToken);
        var hash = PushEndpointIdentity.Hash(endpoint);
        var row = await database.PushDeviceBindings.SingleOrDefaultAsync(x => x.EndpointHash == hash && x.UserId == actor.Id, cancellationToken);
        if (row != null) Deactivate(row);
        await database.SaveChangesAsync(cancellationToken);
        var result = await ProjectAsync(actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    public async Task<PushSettingsDto> DisableAsync(ClaimsPrincipal caller, string? email = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await ActorAsync(caller, email, cancellationToken);
        var preferences = await PreferencesAsync(actor.Id, cancellationToken);
        preferences.MatchEvents = preferences.OpponentMatchEvents = preferences.DeploymentReminder = preferences.EndDay = preferences.MarketEvents =
            preferences.LiveChangesReminder = preferences.LiveFormationChangeEvents = false;
        preferences.Version++;
        foreach (var binding in await database.PushDeviceBindings.Where(x => x.UserId == actor.Id && x.IsActive).ToArrayAsync(cancellationToken)) Deactivate(binding);
        await database.SaveChangesAsync(cancellationToken);
        var result = await ProjectAsync(actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    // A stale provider response cannot erase a refreshed key or a device that
    // moved to another account while an earlier send was in flight.
    public async Task RemoveInvalidAsync(string endpointHash, Guid userId, long expectedVersion, CancellationToken cancellationToken)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var binding = await database.PushDeviceBindings.SingleOrDefaultAsync(x => x.EndpointHash == endpointHash && x.UserId == userId && x.Version == expectedVersion, cancellationToken);
        if (binding is null) return;
        Deactivate(binding);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task<AccountIdentityDto> ActorAsync(ClaimsPrincipal caller, string? email, CancellationToken cancellationToken)
    {
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        if (email != null && !string.Equals(email.Trim(), actor.Email, StringComparison.OrdinalIgnoreCase))
            throw new ApplicationFault(403, "push_account_access_denied", "Puoi gestire soltanto le tue notifiche.");
        return actor;
    }
    private async Task<UserPushPreferences> PreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await database.UserPushPreferences.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (row is null) database.UserPushPreferences.Add(row = new() { UserId = userId });
        return row;
    }
    private async Task<PushSettingsDto> ProjectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await database.UserPushPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken) ?? new() { UserId = userId, Version = 0 };
        var devices = await database.PushDeviceBindings.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).OrderBy(x => x.EndpointHash)
            .Select(x => new PushSubscriptionDto(x.Endpoint, x.P256dh, x.Auth, x.Version, x.IsValid)).ToArrayAsync(cancellationToken);
        return new(row.MatchEvents, row.OpponentMatchEvents, row.DeploymentReminder, row.EndDay, row.MarketEvents, row.LiveChangesReminder, row.LiveFormationChangeEvents,
            devices, row.Version, string.IsNullOrWhiteSpace(options.Value.PublicKey) ? null : options.Value.PublicKey, options.Value.Enabled);
    }
    private static string ProofHash(string proof) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof)));
    private static bool MatchesProof(string? hash, string? proof) => hash != null && proof != null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(ProofHash(proof)));
    private static void Deactivate(PushDeviceBinding row) { row.IsActive = false; row.Auth = ""; row.P256dh = ""; row.DeviceProofHash = null; row.Version++; }
}
