using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fantasoccer.Infrastructure.PostgreSql;

// Every API process names its database sessions after itself. A deploy can cut off the previous process in the
// middle of a transaction: the server keeps that dead session, and the global write lock it holds, until its TCP
// keepalive gives up. The new process releases what the old one left behind instead of waiting for that.
public static class AbandonedSessionReaper
{
    public const string ApplicationNamePrefix = "fantasoccer-api:";

    // One name per process: a restarted process never mistakes its own sessions for abandoned ones.
    public static readonly string OwnApplicationName = ApplicationNamePrefix + Guid.NewGuid().ToString("N")[..12];

    public static string WithApplicationName(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.ApplicationName)) builder.ApplicationName = OwnApplicationName;
        // A connection whose packets are dropped (seen right after the deploy swap) is noticed by TCP keepalive instead of only by the
        // 120 s command timeout: first probe after 10 s of silence, then every 5 s.
        if (!builder.TcpKeepAlive) { builder.TcpKeepAlive = true; builder.TcpKeepAliveTime = 10; builder.TcpKeepAliveInterval = 5; }
        return builder.ConnectionString;
    }

    // Ends the sessions of other API processes whose transaction has been open longer than a healthy one ever is.
    public static async Task<int> TerminateAsync(FantasoccerDbContext database, string ownApplicationName, TimeSpan minimumTransactionAge, CancellationToken cancellationToken)
    {
        var seconds = Math.Max(0, minimumTransactionAge.TotalSeconds);
        var pattern = ApplicationNamePrefix + "%";
        return (await database.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM (
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = current_database() AND usename = current_user AND pid <> pg_backend_pid()
                  AND application_name LIKE {pattern} AND application_name <> {ownApplicationName}
                  AND xact_start IS NOT NULL AND xact_start < now() - make_interval(secs => {seconds})) ended
            """).ToListAsync(cancellationToken)).Single();
    }
}
