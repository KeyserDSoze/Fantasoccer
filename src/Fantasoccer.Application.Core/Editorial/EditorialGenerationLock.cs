using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Editorial;

// A bounded session lock, not a gameplay transaction held across provider I/O.
// Concurrent clicks for the same resource cannot multiply paid generation calls.
internal sealed class EditorialGenerationLock(FantasoccerDbContext database, long key) : IAsyncDisposable
{
    public static async Task<EditorialGenerationLock> AcquireAsync(FantasoccerDbContext database, string resource, CancellationToken cancellationToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(resource));
        var key = (BinaryPrimitives.ReadInt64BigEndian(bytes) & 0x00FFFFFFFFFFFFFFL) | 0x6E00000000000000L;
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)"; command.CommandTimeout = 5;
            var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.DbType = DbType.Int64; parameter.Value = key; command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) is not true)
                throw new ApplicationFault(409, "editorial_generation_running", "Una generazione è già in corso per questo contenuto.");
            return new(database, key);
        }
        catch { await database.Database.CloseConnectionAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@key)"; command.CommandTimeout = 5;
            var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.DbType = DbType.Int64; parameter.Value = key; command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync();
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }
}
