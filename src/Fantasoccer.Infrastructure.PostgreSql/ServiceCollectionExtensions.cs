using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Infrastructure.PostgreSql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFantasoccerPostgreSql(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<FantasoccerDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.CommandTimeout(120)));
        return services;
    }

    // Call before RunAsync/StartAsync, warm-up and background jobs. Importing legacy
    // data is intentionally NOT part of startup. Keep an outer session lock across
    // history discovery AND all migrations, not just individual migration commands.
    public static async Task MigrateFantasoccerDatabaseAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
        await database.Database.OpenConnectionAsync(cancellationToken);
        var acquired = false;
        try
        {
            await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(59127502874018)", cancellationToken);
            acquired = true;
            await database.Database.MigrateAsync(cancellationToken);
        }
        catch
        {
            // Cancellation can race with the server granting the session lock.
            // If acquisition did not return successfully, discard that session.
            if (!acquired) Npgsql.NpgsqlConnection.ClearPool((Npgsql.NpgsqlConnection)database.Database.GetDbConnection());
            throw;
        }
        finally
        {
            try
            {
                if (acquired)
                    await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(59127502874018)", CancellationToken.None);
            }
            catch
            {
                // A failed unlock must never return a locked physical session to the pool.
                Npgsql.NpgsqlConnection.ClearPool((Npgsql.NpgsqlConnection)database.Database.GetDbConnection());
                throw;
            }
            finally { await database.Database.CloseConnectionAsync(); }
        }
    }
}
