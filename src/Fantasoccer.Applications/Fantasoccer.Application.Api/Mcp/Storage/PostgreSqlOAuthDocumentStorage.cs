using System.Runtime.CompilerServices;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fantasoccer.Application.Api.Mcp.Storage;

// Singleton consumers (consent transactions/retention) must not capture a scoped
// DbContext. Each operation opens its own scope and atomic SQL compare-and-swap.
public sealed class PostgreSqlOAuthDocumentStorage(IServiceScopeFactory scopes) : IOAuthDocumentStorage
{
    public async Task<OAuthDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>().OAuthRecords.AsNoTracking()
            .Where(x => x.Id == id).Select(x => new OAuthDocument
            { Id = x.Id, Kind = x.Kind, Version = x.Version, Payload = x.Payload, DeleteAfterUtc = x.DeleteAfterUtc }).SingleOrDefaultAsync(cancellationToken);
    }

    public async IAsyncEnumerable<OAuthDocument> ListAsync(string kind, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var records = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>().OAuthRecords.AsNoTracking().Where(x => x.Kind == kind && x.Payload != null);
        await foreach (var row in records.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return new() { Id = row.Id, Kind = row.Kind, Version = row.Version, Payload = row.Payload, DeleteAfterUtc = row.DeleteAfterUtc };
    }

    public async Task<bool> WriteAsync(OAuthDocument document, string? expectedVersion, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        if (expectedVersion is not null)
        {
            var changed = await database.OAuthRecords.Where(x => x.Id == document.Id && x.Version == expectedVersion)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Version, document.Version)
                    .SetProperty(x => x.Kind, document.Kind).SetProperty(x => x.Payload, document.Payload)
                    .SetProperty(x => x.DeleteAfterUtc, document.DeleteAfterUtc), cancellationToken) == 1;
            if (!changed) return false;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        database.OAuthRecords.Add(new() { Id = document.Id, Kind = document.Kind, Version = document.Version, Payload = document.Payload, DeleteAfterUtc = document.DeleteAfterUtc });
        try { await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return true; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }

    public async Task<long> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var removed = await database.OAuthRecords
            .Where(x => x.DeleteAfterUtc != null && x.DeleteAfterUtc <= now).ExecuteDeleteAsync(cancellationToken);
        if (removed > 0) { await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        return removed;
    }
}
