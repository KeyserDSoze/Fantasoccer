using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Fantasoccer.LegacyModels;
using Fantasoccer.StorageMigration;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class PlayerCatalogResolutionConfigurationTests
{
    [Fact]
    public void Catalog_rules_are_explicit_checksum_pins_with_positive_expected_counts_and_no_overlapping_hashes()
    {
        Assert.Empty(PlayerCatalogResolutionConfiguration.Parse(null)); Assert.Empty(PlayerCatalogResolutionConfiguration.Parse(""));
        Assert.Empty(PlayerCatalogResolutionConfiguration.Parse("[]"));
        var rule = Assert.Single(PlayerCatalogResolutionConfiguration.Parse(JsonSerializer.Serialize(new[] { new PlayerCatalogResolution(new string('A', 64), 2) })));
        Assert.Equal(new string('a', 64), rule.DocumentSha256); Assert.Equal(2, rule.ExpectedUnnamedPlayers);
        foreach (var invalid in new[] { "null", "{}", "[{}]", "[null]", "[{\"documentSha256\":\"private-invalid-pin\",\"expectedUnnamedPlayers\":2}]", "[{\"skipAllInvalidPlayers\":true}]" })
        {
            var fault = Assert.Throws<CoreImportException>(() => PlayerCatalogResolutionConfiguration.Parse(invalid));
            Assert.Equal("invalid_player_catalog_resolution_config", fault.Message);
            Assert.DoesNotContain("private-invalid-pin", CoreImportDiagnostics.Format(fault));
        }
        foreach (var count in new[] { 0, -1, 100001 })
            Assert.Throws<CoreImportException>(() => PlayerCatalogResolutionConfiguration.Normalize([rule with { ExpectedUnnamedPlayers = count }]));
        Assert.Throws<CoreImportException>(() => PlayerCatalogResolutionConfiguration.Normalize([rule, rule]));
    }
}

public sealed partial class CoreMigrationTests
{
    private const string UnnamedCatalogRow = """{"n":"","t":{"n":"Fixture club"},"r":3,"a":true,"future":{"note":"private-catalog-note","number":1e0,"extra":1,"extra":2}}""";
    private static string CatalogDocument(params string[] rows) => "{\"Key\":15,\"Value\":{\"p\":[" + string.Join(',', rows) + "]}}";
    private static string NamedCatalogRow(string name = "Named forward", int role = 3) => JsonSerializer.Serialize(new RealPlayer
    { Name = name, RealTeam = new() { Name = "Fixture club" }, Role = (Role)role, IsActive = true });

    [CoreFact]
    public async Task Approved_catalog_resolution_preserves_each_unnamed_row_exactly_without_fake_players_or_alias_merging_and_is_atomic_and_idempotent()
    {
        var json = CatalogDocument(UnnamedCatalogRow, NamedCatalogRow(), UnnamedCatalogRow, NamedCatalogRow("Named forward&#x27;"));
        var (manifest, rule) = await WriteCatalogResolutionSourceAsync(json, 2);
        var options = new CoreImportOptions(PlayerCatalogResolutions: PlayerCatalogResolutionConfiguration.Parse(JsonSerializer.Serialize(new[] { rule })));
        var source = _storage.Blobs.GetBlobContainerClient("realplayerswrapper").GetBlobClient("catalog");
        var etag = (await source.GetPropertiesAsync()).Value.ETag;
        await using var database = fixture.CreateContext();
        var (_, preview) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        Assert.Equal(2, preview.UnresolvedPlayerCatalogEntries.Count);
        Assert.False(Assert.Single(preview.ResolvedPlayerCatalogs).ExplicitApproval);
        Assert.Empty(await database.Players.ToArrayAsync());
        await using (var interrupted = new FantasoccerDbContext(new DbContextOptionsBuilder<FantasoccerDbContext>()
            .UseNpgsql(database.Database.GetConnectionString()).AddInterceptors(new RejectCommit()).Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CoreDatasetImporter(interrupted, options).ImportAsync(_dump));
        Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.UnresolvedPlayerCatalogEntries.ToArrayAsync());
        var report = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        var applied = Assert.Single(report.ResolvedPlayerCatalogs!);
        Assert.Equal(rule.DocumentSha256, applied.DocumentSha256); Assert.Equal(4, applied.TotalEntries);
        Assert.Equal(2, applied.ImportedEntries); Assert.Equal(2, applied.UnresolvedEntries); Assert.Equal(15, applied.SeasonIndex);
        Assert.Equal(2, report.UnresolvedPlayerCatalogEntries); Assert.False(report.FullMigrationComplete);
        Assert.Contains(report.PendingFields, x => x.Contains("not imported as usable players"));
        Assert.Contains(report.PreservedArtifacts!, x => x.Contains(rule.DocumentSha256) && x.Contains("SQL"));
        var rows = await database.UnresolvedPlayerCatalogEntries.AsNoTracking().OrderBy(x => x.SourceOrdinal).ToArrayAsync();
        Assert.Equal(new[] { 0, 2 }, rows.Select(x => x.SourceOrdinal)); Assert.NotEqual(rows[0].Id, rows[1].Id);
        foreach (var row in rows)
        {
            Assert.Equal(UnnamedCatalogRow, row.OriginalPayloadJson); Assert.Equal("empty_player_name", row.Reason);
            Assert.Equal(rule.DocumentSha256, row.DocumentSha256); Assert.Equal(manifest.SourceId, row.SourceId);
            Assert.Equal("$.Value.p[" + row.SourceOrdinal + "]", row.JsonPath); Assert.Equal(applied.ArchiveReference, row.ArchiveReference);
            Assert.True(File.Exists(Path.Combine(_dump, row.ArchiveReference)));
            using var payload = JsonDocument.Parse(row.OriginalPayloadJson);
            Assert.True(payload.RootElement.GetProperty("a").GetBoolean());
        }
        var names = await database.Players.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync();
        Assert.Equal(new[] { "Named forward", "Named forward&#x27;" }, names); // No HTML/key normalization or alias merging.
        Assert.Equal(2, await database.PlayerSeasons.CountAsync());
        Assert.Equal(2, await database.LegacyIdentityMaps.CountAsync(x => x.Kind == "unresolved-player-catalog-entry"));
        var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        Assert.Equal(rows.Select(x => x.Id).Order(), (await database.UnresolvedPlayerCatalogEntries.Select(x => x.Id).ToArrayAsync()).Order());
        Assert.Equal(etag, (await source.GetPropertiesAsync()).Value.ETag);
        Assert.Equal(json, (await source.DownloadContentAsync()).Value.Content.ToString());
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(_dump, applied.ArchiveReference)));
        Assert.DoesNotContain("private-catalog-note", JsonSerializer.Serialize(report));
        Assert.DoesNotContain("Fixture club", JsonSerializer.Serialize(report));
        var automatic = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(automatic.Documents, automatic.SkippedDocuments);
        Assert.False(Assert.Single(automatic.ResolvedPlayerCatalogs!).ExplicitApproval);
        Assert.Equal(2, await database.UnresolvedPlayerCatalogEntries.CountAsync());
        // Downgrades must refuse to drop archived anomalies with source data.
        await Assert.ThrowsAsync<PostgresException>(() => database.GetService<IMigrator>().MigrateAsync("20261026172736_HistoricalParticipantAccounts"));
        Assert.Equal(2, await database.UnresolvedPlayerCatalogEntries.CountAsync());
    }

    [CoreFact]
    public async Task Catalog_resolution_refuses_stale_missing_wrong_scope_ambiguous_and_wrong_count_approvals()
    {
        var (manifest, rule) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow()), 1);
        var groupHash = manifest.Containers.Single(x => x.Name == "group").Blobs[0].Sha256;
        foreach (var (choice, expected) in new[]
        {
            (rule with { DocumentSha256 = new string('0', 64) }, "player_catalog_resolution_document_not_found"),
            (rule with { DocumentSha256 = groupHash }, "player_catalog_resolution_document_not_found"),
            (rule with { ExpectedUnnamedPlayers = 2 }, "player_catalog_resolution_count_mismatch")
        })
            Assert.Equal(expected, (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump,
                options: new(PlayerCatalogResolutions: [choice])))).Message);
        var container = _storage.Blobs.GetBlobContainerClient("realplayerswrapper");
        await container.GetBlobClient("other-copy").UploadAsync((await container.GetBlobClient("catalog").DownloadContentAsync()).Value.Content);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        Assert.Equal("player_catalog_resolution_ambiguous", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump,
            options: new(PlayerCatalogResolutions: [rule])))).Message);
        await container.DeleteBlobAsync("other-copy");
        await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow("Changed after approval")), 1);
        Assert.Equal("player_catalog_resolution_document_not_found", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump,
            options: new(PlayerCatalogResolutions: [rule])))).Message);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
        Assert.Empty(await database.UnresolvedPlayerCatalogEntries.ToArrayAsync());
    }

    [CoreFact]
    public async Task Unnamed_catalog_rows_are_archived_including_raw_keys_but_never_become_usable_players()
    {
        foreach (var row in new[]
        {
            "{\"n\":\"\",\"r\":99}",
            "{\"n\":\"\",\"r\":3,\"key\":\"independentplayerkey\"}",
            "{\"n\":null,\"r\":3}",
            "{\"n\":\" \",\"r\":3}",
            "null"
        })
        {
            var (_, rule) = await WriteCatalogResolutionSourceAsync(CatalogDocument(row, NamedCatalogRow()), 1);
            var (_, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump, options: new(PlayerCatalogResolutions: [rule]));
            Assert.Equal(row, Assert.Single(plan.UnresolvedPlayerCatalogEntries).OriginalPayloadJson);
            Assert.Single(plan.Players); Assert.All(plan.Players, player => Assert.False(string.IsNullOrWhiteSpace(player.Name)));
        }
        foreach (var bad in new[] { NamedCatalogRow("Named but invalid role", 99), NamedCatalogRow("12345") })
        {
            var (_, rule) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, bad), 1);
            Assert.Equal("invalid_player", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump,
                options: new(PlayerCatalogResolutions: [rule])))).Message);
        }
        await using var database = fixture.CreateContext(); Assert.Empty(await database.UnresolvedPlayerCatalogEntries.ToArrayAsync());
    }

    [CoreFact]
    public async Task A_nameless_roster_still_blocks_while_other_unnamed_catalogs_are_archived_without_changing_valid_players()
    {
        var (_, rule) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow()), 1);
        var options = new CoreImportOptions(PlayerCatalogResolutions: [rule]);
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        var original = await database.UnresolvedPlayerCatalogEntries.AsNoTracking().SingleAsync();
        var rosters = await ExtraContainerAsync("team");
        await rosters.GetBlobClient("unnamed-roster").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Team, TeamKey>.Default(
            new() { Name = "Roster", Owner = Email, Players = [new() { Name = "", Role = Role.Forward, RealTeam = new() { Name = "Fixture club" } }] },
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database, options).ImportAsync(_dump));
        Assert.Equal("invalid_player", fault.Message); Assert.Equal("team", fault.PlayerValidation!.Container);
        await rosters.DeleteBlobAsync("unnamed-roster");
        await _storage.Blobs.GetBlobContainerClient("realplayerswrapper").GetBlobClient("unapproved").UploadAsync(BinaryData.FromString(
            "{\"Key\":14,\"Value\":{\"p\":[" + UnnamedCatalogRow + "]}}"));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var report = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(2, report.UnresolvedPlayerCatalogEntries);
        Assert.Contains(report.ResolvedPlayerCatalogs!, x => !x.ExplicitApproval && x.SeasonIndex == 14);
        Assert.Equal(original.OriginalPayloadJson, (await database.UnresolvedPlayerCatalogEntries.AsNoTracking().SingleAsync(x => x.Id == original.Id)).OriginalPayloadJson);
        Assert.Equal(1, await database.Players.CountAsync());
    }

    [CoreFact]
    public async Task Catalog_resolution_preserves_modern_envelopes_and_accepts_normalized_checksum_pins()
    {
        var json = CatalogDocument(UnnamedCatalogRow, NamedCatalogRow()).Replace("\"Key\":", "\"k\":").Replace("\"Value\":", "\"v\":");
        var (_, rule) = await WriteCatalogResolutionSourceAsync(json, 1);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database, new(PlayerCatalogResolutions: [rule with { DocumentSha256 = rule.DocumentSha256.ToUpperInvariant() }])).ImportAsync(_dump);
        var row = await database.UnresolvedPlayerCatalogEntries.SingleAsync();
        Assert.Equal("$.v.p[0]", row.JsonPath); Assert.Equal(UnnamedCatalogRow, row.OriginalPayloadJson);
        Assert.Equal(rule.DocumentSha256, row.DocumentSha256); Assert.Equal(1, report.UnresolvedPlayerCatalogEntries);
    }

    [CoreFact]
    public async Task Catalog_resolution_does_not_overwrite_unowned_archive_rows_in_a_nonempty_target()
    {
        var (_, rule) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow()), 1);
        await using var database = fixture.CreateContext();
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 });
        var id = Guid.NewGuid();
        database.UnresolvedPlayerCatalogEntries.Add(new()
        {
            Id = id, SourceId = "unowned-fixture", DocumentSha256 = new string('a', 64), ArchiveReference = "blobs/" + new string('b', 64) + ".bin",
            JsonPath = "$.v.p[0]", SourceOrdinal = 0, SeasonIndex = 15, Reason = "empty_player_name", OriginalPayloadJson = UnnamedCatalogRow
        });
        await database.SaveChangesAsync();
        Assert.Equal("target_is_not_empty", (await Assert.ThrowsAsync<CoreImportException>(() =>
            new CoreDatasetImporter(database, new(PlayerCatalogResolutions: [rule])).ImportAsync(_dump))).Message);
        Assert.Equal(id, (await database.UnresolvedPlayerCatalogEntries.AsNoTracking().SingleAsync()).Id);
        Assert.Empty(await database.Users.ToArrayAsync()); Assert.Empty(await database.Players.ToArrayAsync());
    }

    [CoreFact]
    public async Task New_catalog_approval_replaces_only_its_source_anomalies_and_cannot_reopen_import_after_application_writes()
    {
        var (_, initial) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow(), UnnamedCatalogRow), 2);
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database, new(PlayerCatalogResolutions: [initial])).ImportAsync(_dump);
        var playerId = (await database.Players.SingleAsync()).Id;
        var oldIds = await database.UnresolvedPlayerCatalogEntries.Select(x => x.Id).ToArrayAsync();
        var (_, changed) = await WriteCatalogResolutionSourceAsync(CatalogDocument(UnnamedCatalogRow, NamedCatalogRow()), 1);
        Assert.Equal("player_catalog_resolution_document_not_found", (await Assert.ThrowsAsync<CoreImportException>(() =>
            new CoreDatasetImporter(database, new(PlayerCatalogResolutions: [initial])).ImportAsync(_dump))).Message);
        Assert.Equal(2, await database.UnresolvedPlayerCatalogEntries.CountAsync());
        var approved = new CoreImportOptions(PlayerCatalogResolutions: [changed]);
        await new CoreDatasetImporter(database, approved).ImportAsync(_dump);
        Assert.Equal(playerId, (await database.Players.SingleAsync()).Id);
        var current = await database.UnresolvedPlayerCatalogEntries.AsNoTracking().SingleAsync();
        Assert.DoesNotContain(current.Id, oldIds); Assert.Equal(changed.DocumentSha256, current.DocumentSha256);
        await using (var interactive = fixture.CreateContext())
        {
            await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(interactive, default);
            await interactive.SaveChangesAsync(); await transaction.CommitAsync();
        }
        Assert.Equal("interactive_writes_already_started", (await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database, approved).ImportAsync(_dump))).Message);
        Assert.Equal(current.Id, (await database.UnresolvedPlayerCatalogEntries.AsNoTracking().SingleAsync()).Id);
    }

    private async Task<(DumpManifest Manifest, PlayerCatalogResolution Rule)> WriteCatalogResolutionSourceAsync(string json, int expected)
    {
        var container = _extraContainers.Contains("realplayerswrapper") ? _storage.Blobs.GetBlobContainerClient("realplayerswrapper") : await ExtraContainerAsync("realplayerswrapper");
        await container.GetBlobClient("catalog").UploadAsync(BinaryData.FromString(json), overwrite: true);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        return (manifest, new(manifest.Containers.Single(x => x.Name == "realplayerswrapper").Blobs.Single(x => x.Name == "catalog").Sha256, expected));
    }
}
