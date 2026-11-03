using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class AbandonedSessionReaperTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task A_new_process_releases_the_global_lock_held_by_the_previous_one_and_leaves_healthy_sessions_alone()
    {
        await using var probe = database.CreateContext();
        var text = probe.Database.GetConnectionString()!;
        NpgsqlConnection Open(string name) { var builder = new NpgsqlConnectionStringBuilder(text) { ApplicationName = name, Pooling = false }; var c = new NpgsqlConnection(builder.ConnectionString); c.Open(); return c; }
        var previous = AbandonedSessionReaper.ApplicationNamePrefix + "previous000";
        await using var abandoned = Open(previous); await using var healthy = Open(AbandonedSessionReaper.OwnApplicationName); await using var other = Open("not-ours");
        foreach (var connection in new[] { abandoned, healthy, other })
        {
            await using var begin = connection.CreateCommand(); begin.CommandText = "BEGIN; SELECT pg_advisory_xact_lock(@key)";
            begin.Parameters.AddWithValue("key", connection == abandoned ? 59127502874019L : connection == healthy ? 59127502874020L : 59127502874021L);
            await begin.ExecuteNonQueryAsync();
        }

        // A transaction that is still young is a healthy one (the previous process may be finishing its last request).
        Assert.Equal(0, await AbandonedSessionReaper.TerminateAsync(probe, AbandonedSessionReaper.OwnApplicationName, TimeSpan.FromMinutes(5), default));
        Assert.Equal(1, await AbandonedSessionReaper.TerminateAsync(probe, AbandonedSessionReaper.OwnApplicationName, TimeSpan.Zero, default));

        // The lock of the abandoned session is free again; the process's own session and a foreign application are untouched.
        await using var waiter = Open(AbandonedSessionReaper.OwnApplicationName);
        await using var lockCommand = waiter.CreateCommand(); lockCommand.CommandText = "SELECT pg_try_advisory_lock(59127502874019)";
        Assert.True((bool)(await lockCommand.ExecuteScalarAsync())!);
        await using var alive = healthy.CreateCommand(); alive.CommandText = "SELECT 1";
        Assert.Equal(1, await alive.ExecuteScalarAsync());
        await using var foreign = other.CreateCommand(); foreign.CommandText = "SELECT 1";
        Assert.Equal(1, await foreign.ExecuteScalarAsync());
    }
}
