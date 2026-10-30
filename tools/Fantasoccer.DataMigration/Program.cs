using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Fantasoccer.DataMigration;
using System.Text.Json;

if (args is not ["migrate-schema"] && args is not ["import-core", "--directory", _] && args is not ["analyze-core", "--directory", _] &&
    args is not ["seed-operational-cache", "--directory", _, "--season", _, "--day", _])
{
    Console.WriteLine("Usage: migrate-schema | analyze-core --directory /dump | import-core --directory /dump. Review pendingFields, preservedArtifacts, resolvedIdentityConflicts, resolvedPlayerCatalogs, recoveredRosterPlayers, resolvedClubCodes, clubCatalogueYears, missingVoteClubSnapshots, skippedUnnamedVotes, legacyPoliticalVotes, skippedInactiveSubstitutes, skippedUnusablePlayerRows, excludedNamelessRosterEntries, missingRosterClubSnapshots, excludedRealCalendars and discardedFutureScores; the report cannot authorize production cutover. FANTASOCCER_IMPORT_LEGACY_AUCTIONS=false omits old auctions/orders; FANTASOCCER_IMPORT_LEGACY_LOGS=true opts into historical Table logs (default false). FANTASOCCER_IDENTITY_RESOLUTIONS_JSON supplies checksum-pinned identity choices. FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS=true preserves non-email historical users. Unnamed player catalog rows are archived, not usable players; optional FANTASOCCER_PLAYER_CATALOG_RESOLUTIONS_JSON asserts reviewed document hashes/counts. Club codes and official names use the offline, versioned catalogue; explicit conflicts remain errors. ClubSeasons records only attested membership. Named minimal legacy votes/baselines preserve NULL historical clubs and original flags; same-year season rosters may complete only an unambiguous missing annual club. Unnamed legacy votes/baselines are excluded from SQL, not archived there, and counted in skippedUnnamedVotes; original dump bytes remain unchanged. Legacy political 6 substitutes attested by the calendar or by an actual vote of the same player/club are kept apart in LegacyPoliticalVoteEntries, never as actual votes; two actual votes for one player remain an error. Official 6 d'ufficio entries given to inactive players are not votes: they are excluded from SQL and counted in skippedInactiveSubstitutes. Lineup and statistics rows without a usable name are excluded (skippedUnusablePlayerRows); sold or removed roster entries without any name or stored key are excluded with their money listed in excludedNamelessRosterEntries, while active nameless entries remain errors. Legacy roster and hall entries without any club keep a NULL club (missingRosterClubSnapshots), never filled from the catalogue. Real calendars whose dated games all belong to another season are excluded from SQL and listed in excludedRealCalendars; mixed calendars are refused. Scores on games scheduled after the export started are imported as no result and counted in discardedFutureScores.");
    Console.WriteLine("seed-operational-cache --directory /dump --season N --day D publishes that matchday's legacy probable lineups and provisional live votes to the shared Blob cache (FANTASOCCER_CACHE_STORAGE, FANTASOCCER_CACHE_NAMESPACE, optional FANTASOCCER_CACHE_CONTAINER). They are never imported into PostgreSQL; newer provider snapshots are never overwritten.");
    Console.WriteLine("Only the current fantasy rank is stored (SnapshotDay=0). Legacy dailyrank blobs are verified and preserved in the dump but excluded from SQL; historical API ranks are reconstructed from saved calendar/results and annual rules without scoring votes or crediting money. Existing SQL daily copies refuse schema upgrade until explicitly reconciled after a verified target backup.");
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
    if (!bool.TryParse(Environment.GetEnvironmentVariable("FANTASOCCER_IDENTITY_PREFER_LARGEST_DOCUMENT") ?? "false", out var preferLargestIdentity))
        throw new InvalidOperationException("FANTASOCCER_IDENTITY_PREFER_LARGEST_DOCUMENT must be true or false.");
    var importOptions = new CoreImportOptions(Environment.GetEnvironmentVariable("FANTASOCCER_LEGACY_ENCRYPTION_KEY"),
        Environment.GetEnvironmentVariable("FANTASOCCER_LEGACY_ENCRYPTION_IV"), importLegacyAuctions, importLegacyLogs,
        IdentityResolutionConfiguration.Parse(Environment.GetEnvironmentVariable("FANTASOCCER_IDENTITY_RESOLUTIONS_JSON")), importLegacyNamedOwners,
        PlayerCatalogResolutionConfiguration.Parse(Environment.GetEnvironmentVariable("FANTASOCCER_PLAYER_CATALOG_RESOLUTIONS_JSON")),
        preferLargestIdentity);
    if (args[0] == "analyze-core")
    {
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(args[2], options: importOptions);
        Console.WriteLine(JsonSerializer.Serialize(CoreDatasetImporter.Report(dataset, plan), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "seed-operational-cache")
    {
        if (!int.TryParse(args[4], out var season) || !int.TryParse(args[6], out var day) || season is < 1 or > 200 || day is < 1 or > 38)
            throw new InvalidOperationException("Invalid season or day.");
        var storage = Environment.GetEnvironmentVariable("FANTASOCCER_CACHE_STORAGE") ?? throw new InvalidOperationException("Missing FANTASOCCER_CACHE_STORAGE.");
        var cacheNamespace = Environment.GetEnvironmentVariable("FANTASOCCER_CACHE_NAMESPACE") ?? throw new InvalidOperationException("Missing FANTASOCCER_CACHE_NAMESPACE (it must match the API Cache:Namespace).");
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(args[2], options: importOptions);
        var cacheServices = new ServiceCollection().AddLogging().AddMemoryCache();
        Fantasoccer.Infrastructure.Caching.DistributedCacheServiceCollectionExtensions.AddAzureBlobDistributedCache(cacheServices, options =>
        {
            options.ConnectionString = storage; options.Namespace = cacheNamespace;
            options.ContainerName = Environment.GetEnvironmentVariable("FANTASOCCER_CACHE_CONTAINER") ?? options.ContainerName;
        });
        cacheServices.AddOptions<Fantasoccer.Application.Core.Caching.SharedCacheOptions>();
        cacheServices.AddSingleton<Fantasoccer.Application.Core.Caching.SharedSnapshotCache>();
        cacheServices.AddSingleton<Fantasoccer.Application.Core.Football.IFootballLiveDataStore, Fantasoccer.Application.Core.Football.FootballLiveDataStore>();
        await using var cache = cacheServices.BuildServiceProvider();
        var outcome = await OperationalCacheSeed.PublishAsync(cache.GetRequiredService<Fantasoccer.Application.Core.Football.IFootballLiveDataStore>(), plan, season, day, dataset.Manifest.StartedAtUtc);
        Console.WriteLine(JsonSerializer.Serialize(new { season, day, chances = outcome.Chances?.ToString() ?? "none", liveVotes = outcome.Live?.ToString() ?? "none" }));
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
