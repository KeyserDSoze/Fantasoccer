using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Infrastructure.PostgreSql;

// Callers that isolate failures item by item must let this one through:
// once the lease is gone every further write of the job is refused.
public sealed class SqlJobLeaseLostException() : InvalidOperationException("SQL job lease no longer owned; write refused.");

// Flows through async execution and child DI scopes. Every SQL application write
// rechecks the job's fencing token under the same global lock used for the write.
// A slow/canceled old runner cannot commit after a replacement owns its lease.
public static class SqlJobLeaseFence
{
    private sealed record Ticket(string Name, int Year, Guid LeaseId, Func<DateTimeOffset> Now);
    private static readonly AsyncLocal<Ticket?> Current = new();
    public static IDisposable Enter(string name, int year, Guid leaseId, Func<DateTimeOffset> now)
    {
        var previous = Current.Value;
        Current.Value = new(name, year, leaseId, now);
        return new Restore(previous);
    }
    internal static async Task EnsureOwnedAsync(FantasoccerDbContext database, CancellationToken cancellationToken)
    {
        if (Current.Value is not { } ticket) return;
        var now = ticket.Now();
        if (!await database.SqlJobStates.AsNoTracking().AnyAsync(x => x.Name == ticket.Name && x.SeasonIndex == ticket.Year &&
            x.LeaseId == ticket.LeaseId && x.LeaseUntilUtc > now, cancellationToken))
            throw new SqlJobLeaseLostException();
    }
    private sealed class Restore(Ticket? previous) : IDisposable
    { public void Dispose() => Current.Value = previous; }
}
