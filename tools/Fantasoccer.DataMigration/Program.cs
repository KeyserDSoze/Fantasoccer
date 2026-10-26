using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Fantasoccer.DataMigration;
using System.Text.Json;

if (args is not ["migrate-schema"] && args is not ["import-core", "--directory", _] && args is not ["analyze-core", "--directory", _])
{
    Console.WriteLine("Usage: migrate-schema | analyze-core --directory /dump | import-core --directory /dump. Review pendingFields, pendingContainers, excludedContainers, preservedArtifacts, resolvedIdentityConflicts, historicalNamedUsers and resolvedPlayerCatalogs; the report cannot authorize production cutover. FANTASOCCER_IMPORT_LEGACY_AUCTIONS=false omits old auctions/orders; FANTASOCCER_IMPORT_LEGACY_LOGS=true opts into historical Table logs (default false). FANTASOCCER_IDENTITY_RESOLUTIONS_JSON supplies operator-approved, checksum-pinned identity choices. FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS=true preserves non-email owners as users inactive in their groups (default false). FANTASOCCER_PLAYER_CATALOG_RESOLUTIONS_JSON pins reviewed unnamed catalog rows for unresolved SQL preservation, not usable player identities (default empty).");
    return args is ["--help"] ? 0 : 2;
}

try
{
    if (!bool.TryParse(Environment.GetEnvironmentVariable("FANTASOCCER_IMPORT_LEGACY_AUCTIONS") ?? "true", out var importLegacyAuctions))
        throw new InvalidOperationException("FANTASOCCER_IMPORT_LEGACY_AUCTIONS must be true or false.");
    if (!bool.TryParse(Environment.GetEnvironmentVariable("FANTASOCCER_IMPORT_LEGACY_LOGS") ?? "false", out var importLegacyLogs))
        throw new InvalidOperationException("FANTASOCCER_IMPORT_LEGACY_LOGS must be true or false.");
    if (!bool.TryParse(Environment.GetEnvironmentVariable("FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS") ?? "false", out var importLegacyNamedOwners))
        throw new InvalidOperationException("FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS must be true or false.");
    var importOptions = new CoreImportOptions(Environment.GetEnvironmentVariable("FANTASOCCER_LEGACY_ENCRYPTION_KEY"),
        Environment.GetEnvironmentVariable("FANTASOCCER_LEGACY_ENCRYPTION_IV"), importLegacyAuctions, importLegacyLogs,
        IdentityResolutionConfiguration.Parse(Environment.GetEnvironmentVariable("FANTASOCCER_IDENTITY_RESOLUTIONS_JSON")), importLegacyNamedOwners,
        PlayerCatalogResolutionConfiguration.Parse(Environment.GetEnvironmentVariable("FANTASOCCER_PLAYER_CATALOG_RESOLUTIONS_JSON")));
    if (args[0] == "analyze-core")
    {
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(args[2], options: importOptions);
        Console.WriteLine(JsonSerializer.Serialize(CoreDatasetImporter.Report(dataset, plan), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return 0;
    }
    var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FANTASOCCER_POSTGRES_CONNECTION")
        ?? throw new InvalidOperationException("Missing PostgreSQL connection."));
    if (connection.Host is not ("localhost" or "127.0.0.1" or "::1" or "postgres") ||
        connection.Database != "fantasoccer_local" || connection.Username != "fantasoccer_local")
        throw new InvalidOperationException("Only the dedicated local database is supported by this command.");
    await using var services = new ServiceCollection().AddFantasoccerPostgreSql(connection.ConnectionString).BuildServiceProvider();
    await services.MigrateFantasoccerDatabaseAsync();
    if (args[0] == "import-core")
    {
        await using var scope = services.CreateAsyncScope();
        var report = await new CoreDatasetImporter(scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>(), importOptions).ImportAsync(args[2]);
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    else Console.WriteLine("Local EF schema is up to date. No legacy data has been imported.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception is CoreImportException import
        ? CoreImportDiagnostics.Format(import)
        : $"Operation failed ({exception.GetType().Name}). Connection details and document payloads are not logged.");
    return 1;
}
