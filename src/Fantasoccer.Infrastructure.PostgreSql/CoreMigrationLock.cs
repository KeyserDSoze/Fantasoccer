using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Infrastructure.PostgreSql;

public static class CoreMigrationLock
{
    // Deliberately the same lock in the importer and the first interactive write.
    public static Task AcquireAsync(FantasoccerDbContext database, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(59127502874019)", cancellationToken);

    public static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginInteractiveWriteAsync(
        FantasoccerDbContext database, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireAsync(database, cancellationToken);
            await SqlJobLeaseFence.EnsureOwnedAsync(database, cancellationToken);
            var state = await database.CoreMigrationStates.SingleOrDefaultAsync(cancellationToken);
            if (state is null) database.CoreMigrationStates.Add(state = new());
            state.InteractiveWritesStartedAtUtc ??= DateTimeOffset.UtcNow;
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
}
