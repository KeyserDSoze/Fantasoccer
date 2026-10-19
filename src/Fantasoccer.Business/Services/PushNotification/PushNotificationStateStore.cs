using System.Text.Json;
using RepositoryFramework;

namespace Fantasoccer.Business;

// Callers hold the shared push-job lock. A small private index avoids reading
// every historical season's full vote checkpoint on each minute of dispatch.
public sealed class PushNotificationStateStore(IRepository<PushNotificationSent, YearDayKey> repository)
{
    private static YearDayKey IndexKey => new() { Year = 0, Day = 0 };

    public async Task<PushNotificationSent> GetAsync(YearDayKey key, CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetAsync(key, cancellationToken);
        return stored == null ? new() : Copy(stored);
    }

    public async Task<IReadOnlyList<YearDayKey>> GetPendingDaysAsync(CancellationToken cancellationToken = default)
        => (await GetAsync(IndexKey, cancellationToken)).ActiveOutboxDays;

    public async Task SaveAsync(YearDayKey key, PushNotificationSent record, CancellationToken cancellationToken = default)
    {
        var index = await GetAsync(IndexKey, cancellationToken);
        var indexed = index.ActiveOutboxDays.Any(day => day.Year == key.Year && day.Day == key.Day);
        if (record.PendingNotifications.Count > 0 && !indexed)
        {
            index.ActiveOutboxDays.Add(new() { Year = key.Year, Day = key.Day });
            // Index first: a crash may leave an extra empty entry, never an
            // unindexed committed delivery or an advanced vote checkpoint.
            await SaveRawAsync(IndexKey, index, cancellationToken);
        }
        await SaveRawAsync(key, record, cancellationToken);
        if (record.PendingNotifications.Count == 0 && indexed)
        {
            index.ActiveOutboxDays.RemoveAll(day => day.Year == key.Year && day.Day == key.Day);
            await SaveRawAsync(IndexKey, index, cancellationToken);
        }
    }

    private async Task SaveRawAsync(YearDayKey key, PushNotificationSent record, CancellationToken cancellationToken)
    {
        // Do not hand the repository an object we continue to mutate: in-memory
        // storage otherwise changes a "committed" receipt before Save succeeds.
        var detached = Copy(record);
        var result = await repository.ExistAsync(key, cancellationToken)
            ? await repository.UpdateAsync(key, detached, cancellationToken)
            : await repository.InsertAsync(key, detached, cancellationToken);
        if (result?.IsOk != true) throw new InvalidOperationException("Impossibile salvare la coda delle notifiche push.");
    }

    private static PushNotificationSent Copy(PushNotificationSent value)
        => JsonSerializer.Deserialize<PushNotificationSent>(JsonSerializer.Serialize(value))!;
}
