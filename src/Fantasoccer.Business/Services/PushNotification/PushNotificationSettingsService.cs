using RepositoryFramework;
using WebPush;

namespace Fantasoccer.Business;

public sealed class PushNotificationSettingsService(
    IRepository<PushNotificationSettings, string> repository,
    IPushNotificationLock stateLock)
{
    public const string LockName = "push-settings";

    public async Task<PushNotificationSettings> GetAsync(string email, CancellationToken cancellationToken = default)
        => await GetExistingAsync(email, cancellationToken) ?? new();

    public async Task<PushNotificationSettings?> GetExistingAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var held = await stateLock.AcquireAsync(LockName, cancellationToken);
        var settings = (await ResolveAsync(email, held.CancellationToken)).Settings;
        return settings == null ? null : Copy(settings);
    }

    public Task<PushNotificationSettings> UpdateAsync(string email, PushNotificationPreferences patch, CancellationToken cancellationToken = default)
        => MutateAsync(email, settings =>
        {
            settings.MatchEvents = patch.MatchEvents ?? settings.MatchEvents;
            settings.OpponentMatchEvents = patch.OpponentMatchEvents ?? settings.OpponentMatchEvents;
            settings.DeploymentReminder = patch.DeploymentReminder ?? settings.DeploymentReminder;
            settings.EndDay = patch.EndDay ?? settings.EndDay;
            settings.MarketEvents = patch.MarketEvents ?? settings.MarketEvents;
            settings.LiveChangesReminder = patch.LiveChangesReminder ?? settings.LiveChangesReminder;
            settings.LiveFormationChangeEvents = patch.LiveFormationChangeEvents ?? settings.LiveFormationChangeEvents;
        }, cancellationToken);

    public async Task<PushNotificationSettings> AddDeviceAsync(string email, PushSubscription subscription, string? previousEndpoint = null, CancellationToken cancellationToken = default)
    {
        await using var held = await stateLock.AcquireAsync(LockName, cancellationToken);
        cancellationToken = held.CancellationToken;
        // A browser endpoint belongs to one account. Explicit activation transfers it
        // without exposing any other user's settings to the caller.
        foreach (var entity in await repository.ToListAsync(cancellationToken: cancellationToken))
        {
            if (string.Equals(entity.Key, email, StringComparison.OrdinalIgnoreCase) || entity.Value == null)
                continue;
            var other = Copy(entity.Value);
            if (other.Subscriptions.RemoveAll(s => s.Endpoint == subscription.Endpoint) > 0)
                await SaveAsync(entity.Key!, other, true, cancellationToken);
        }
        return await MutateLockedAsync(email, settings =>
        {
            settings.Subscriptions.RemoveAll(s => s.Endpoint == subscription.Endpoint ||
                (previousEndpoint != null && s.Endpoint == previousEndpoint));
            settings.Subscriptions.Add(new(subscription.Endpoint, subscription.P256DH, subscription.Auth));
        }, cancellationToken);
    }

    public Task<PushNotificationSettings> RemoveDeviceAsync(string email, string endpoint, CancellationToken cancellationToken = default)
        => MutateAsync(email, settings => settings.Subscriptions.RemoveAll(s => s.Endpoint == endpoint), cancellationToken);

    public Task<PushNotificationSettings> DisableAllAsync(string email, CancellationToken cancellationToken = default)
        => MutateAsync(email, settings =>
        {
            settings.MatchEvents = settings.OpponentMatchEvents = settings.DeploymentReminder = settings.EndDay =
                settings.MarketEvents = settings.LiveChangesReminder = settings.LiveFormationChangeEvents = false;
            settings.Subscriptions.Clear();
        }, cancellationToken);

    public async Task RemoveInvalidAsync(string email, PushSubscription invalid, CancellationToken cancellationToken = default)
    {
        await using var held = await stateLock.AcquireAsync(LockName, cancellationToken);
        cancellationToken = held.CancellationToken;
        var resolved = await ResolveAsync(email, cancellationToken);
        email = resolved.Key;
        var current = resolved.Settings;
        if (current == null) return;
        var updated = Copy(current);
        // Do not erase keys that were refreshed while an older send was in flight.
        if (updated.Subscriptions.RemoveAll(s => s.Endpoint == invalid.Endpoint && s.P256DH == invalid.P256DH && s.Auth == invalid.Auth) > 0)
            await SaveAsync(email, updated, true, cancellationToken);
    }

    private async Task<PushNotificationSettings> MutateAsync(string email, Action<PushNotificationSettings> mutation, CancellationToken cancellationToken)
    {
        await using var held = await stateLock.AcquireAsync(LockName, cancellationToken);
        return await MutateLockedAsync(email, mutation, held.CancellationToken);
    }

    private async Task<PushNotificationSettings> MutateLockedAsync(string email, Action<PushNotificationSettings> mutation, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(email, cancellationToken);
        email = resolved.Key;
        var current = resolved.Settings;
        var updated = Copy(current ?? new());
        mutation(updated);
        await SaveAsync(email, updated, current != null, cancellationToken);
        return Copy(updated);
    }

    private async Task SaveAsync(string email, PushNotificationSettings settings, bool exists, CancellationToken cancellationToken)
    {
        var state = exists ? await repository.UpdateAsync(email, settings, cancellationToken)
            : await repository.InsertAsync(email, settings, cancellationToken);
        if (state?.IsOk != true) throw new InvalidOperationException("Impossibile salvare le impostazioni push.");
    }

    private async Task<(string Key, PushNotificationSettings? Settings)> ResolveAsync(string email, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(email, cancellationToken);
        if (settings != null) return (email, settings);
        // Old documents use the e-mail spelling seen at registration. Preserve
        // their key and explicit opt-outs when a login provider changes casing.
        var existing = (await repository.ToListAsync(cancellationToken: cancellationToken))
            .FirstOrDefault(entity => string.Equals(entity.Key, email, StringComparison.OrdinalIgnoreCase));
        return existing?.Value != null ? (existing.Key!, existing.Value) : (email.ToLowerInvariant(), null);
    }

    public static PushNotificationSettings Copy(PushNotificationSettings settings) => new()
    {
        MatchEvents = settings.MatchEvents, OpponentMatchEvents = settings.OpponentMatchEvents,
        DeploymentReminder = settings.DeploymentReminder, EndDay = settings.EndDay, MarketEvents = settings.MarketEvents,
        LiveChangesReminder = settings.LiveChangesReminder, LiveFormationChangeEvents = settings.LiveFormationChangeEvents,
        Subscriptions = (settings.Subscriptions ?? []).Select(s => new PushSubscription(s.Endpoint, s.P256DH, s.Auth)).ToList()
    };
}
