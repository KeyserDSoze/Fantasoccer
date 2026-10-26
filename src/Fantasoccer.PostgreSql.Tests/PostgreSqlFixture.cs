using Fantasoccer.Infrastructure.PostgreSql;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlFixture.EnvironmentVariable)))
            Skip = "Set FANTASOCCER_TEST_POSTGRES explicitly or run scripts/migration/test-local.sh.";
    }
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "FANTASOCCER_TEST_POSTGRES";
    private readonly string _databaseName = $"fantasoccer_test_{Guid.NewGuid():N}";
    private string? _adminConnection;
    private string? _testConnection;
    private bool _created;

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured)) return;

        var connection = new NpgsqlConnectionStringBuilder(configured);
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1" or "postgres" or "test-postgres") ||
            connection.Database != "fantasoccer_migration_tests" || connection.Username != "fantasoccer_test")
            throw new InvalidOperationException("Tests require the dedicated local PostgreSQL environment. Remote or non-test targets are refused.");

        _adminConnection = connection.ConnectionString;
        connection.Database = _databaseName;
        connection.Pooling = false;
        _testConnection = connection.ConnectionString;

        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var create = admin.CreateCommand();
        // The identifier is generated here, not read from configuration or user input.
        create.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await create.ExecuteNonQueryAsync();
        _created = true;

        try
        {
            // Exercise the actual startup helper concurrently on an empty database.
            await using var services = new ServiceCollection().AddFantasoccerPostgreSql(_testConnection)
                .BuildServiceProvider(validateScopes: true);
            await Task.WhenAll(services.MigrateFantasoccerDatabaseAsync(), services.MigrateFantasoccerDatabaseAsync());
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public FantasoccerDbContext CreateContext() => new(new DbContextOptionsBuilder<FantasoccerDbContext>()
        .UseNpgsql(_testConnection ?? throw new InvalidOperationException("PostgreSQL tests are not configured."),
            postgres => postgres.CommandTimeout(120)).Options);

    public async ValueTask DisposeAsync()
    {
        if (!_created) return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        // Only the unique database created by this fixture is removed, never the configured database.
        drop.CommandText = $"DROP DATABASE \"{_databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
        _created = false;
    }
}
