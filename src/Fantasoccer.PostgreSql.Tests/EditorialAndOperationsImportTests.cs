using System.Text.Json;
using Azure.Data.Tables;
using Fantasoccer.DataMigration;
using Fantasoccer.Domain;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Hall_import_initializes_incremental_checkpoint_only_when_available_season_totals_match_authoritative_archive()
    {
        var ranks = await ExtraContainerAsync("rank"); var hall = await ExtraContainerAsync("halloffame");
        var row = new RankedTeam { Name = "Team", Owner = Email, Point = 7.25f, Goal = 2, Money = 10, ValuePoint = 99.5f };
        var rank = new Rank { SerieADay = 3, RankedTeams = new() { ["@"] = [row] } };
        await ranks.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Rank, LeagueKey>.Default(rank, new() { Group = "first", League = "league", Year = 15 }))));
        var archive = new HallOfFame { Ranks = [row], WinningPlayers = [], WinningTeams = [] };
        await hall.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<HallOfFame, GeneralLeagueKey>.Default(archive, new() { Group = "first", League = "league" }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true); await ImportAsync();
        await using var database = fixture.CreateContext();
        Assert.True((await database.HallArchives.SingleAsync()).ContributionsInitialized);
        Assert.Equal(7.25f, (await database.HallRankContributions.SingleAsync()).Points);
        rank.RankedTeams["@"][0] = new() { Name = "Team", Owner = Email, Point = 10, Goal = 2, Money = 10, ValuePoint = 99.5f };
        await ranks.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Rank, LeagueKey>.Default(rank, new() { Group = "first", League = "league", Year = 15 }))), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true); await new CoreDatasetImporter(database).ImportAsync(_dump);
        var preserved = await database.HallArchives.SingleAsync();
        Assert.False(preserved.ContributionsInitialized); Assert.Equal(7.25f, preserved.Ranks.Single().Points);
        Assert.Empty(await database.HallRankContributions.ToArrayAsync());
    }

    [CoreFact]
    public async Task Preserved_blob_files_and_derived_archives_are_reported_explicitly_but_unknown_containers_remain_pending()
    {
        foreach (var name in new[] { "images", "livegroup", "unrecognizedfuturedata" })
        {
            var container = await ExtraContainerAsync(name);
            await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString("original payload bytes"));
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("images:"));
        Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("livegroup:"));
        Assert.DoesNotContain("images", report.PendingContainers); Assert.DoesNotContain("livegroup", report.PendingContainers);
        Assert.Contains("unrecognizedfuturedata", report.PendingContainers); Assert.False(report.FullMigrationComplete);
        await _storage.Blobs.GetBlobContainerClient("livegroup").DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.DoesNotContain(repeated.PreservedArtifacts!, x => x.StartsWith("livegroup:"));
    }

    [CoreFact]
    public async Task Editorial_import_preserves_HTML_dates_and_owner_scoped_advice_without_generating_or_inventing_a_league()
    {
        var news = await ExtraContainerAsync("news"); var advice = await ExtraContainerAsync("teamwithai");
        var published = new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc);
        var edition = new NewsWrapper { Year = 15, Day = 4, News = [new() { League = "League", Title = "Original title", Content = "<p>Historical content &amp; facts</p>", Date = published, Image = "https://example.test/image.png" },
            new() { League = "Unresolved historical label", Title = "Standalone article", Content = "Original standalone content", Date = published }] };
        await news.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<NewsWrapper, YearDayGroup>.Default(edition, new() { Group = "first", Year = 15, Day = 4 }))));
        await advice.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<TeamWithAi, TeamDayKey>.Default(new() { Message = "Private saved advice" },
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Day = 4, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var saved = await database.NewsEditions.SingleAsync();
        var articles = saved.Articles.OrderBy(x => x.Ordinal).ToArray();
        Assert.Equal(edition.News[0].Content, articles[0].Content); Assert.NotNull(articles[0].LeagueId); Assert.Null(articles[1].LeagueId);
        Assert.Equal(new DateTimeOffset(published), articles[0].PublishedAtUtc); Assert.False(articles[0].IsGenerated);
        var suggestion = await database.TeamAdvice.SingleAsync(); Assert.Equal("Private saved advice", suggestion.Message);
        Assert.Equal((await database.Users.SingleAsync(x => x.Email == Email)).Id, suggestion.OwnerUserId);
        Assert.False(suggestion.IsGenerated); Assert.Null(suggestion.GeneratedByUserId);
        Assert.Equal(suggestion.TeamId, (await database.TeamSeasons.SingleAsync(x => x.Id == suggestion.TeamId)).Id);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump); Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await news.DeleteBlobAsync("sample"); await advice.DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true); await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.NewsEditions.ToArrayAsync()); Assert.Empty(await database.TeamAdvice.ToArrayAsync());
    }

    [CoreFact]
    public async Task Table_logs_import_preserves_typed_properties_timestamp_ETag_and_row_identity_and_reconciles_removal()
    {
        var table = _storage.Tables.GetTableClient("logs");
        Assert.False(await TableExistsAsync()); await table.CreateAsync();
        try
        {
            var options = new CoreImportOptions(ImportLegacyLogs: true);
            await table.AddEntityAsync(new TableEntity("ALog", "SourceJob_12") { ["Message"] = "Historical operational message", ["Counter"] = 9L, ["Binary"] = new byte[] { 1, 2, 3 }, ["Enabled"] = false });
            var source = (await table.GetEntityAsync<TableEntity>("ALog", "SourceJob_12")).Value;
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true, includeTables: true);
            await using var database = fixture.CreateContext();
            await new CoreDatasetImporter(database, options).ImportAsync(_dump);
            var log = await database.ArchivedOperationalLogs.SingleAsync(); var id = log.Id;
            Assert.Equal(source.Timestamp!.Value.UtcTicks, log.LegacyTimestampUtcTicks);
            Assert.InRange(Math.Abs(source.Timestamp.Value.UtcTicks - log.TimestampUtc!.Value.UtcTicks), 0, 9);
            Assert.Equal(source.ETag.ToString(), log.ETag);
            Assert.Equal("Historical operational message", log.Message);
            Assert.Equal("Edm.Int64", log.Properties.Single(x => x.Name == "Counter").Type);
            Assert.Equal("\"AQID\"", log.Properties.Single(x => x.Name == "Binary").EncodedValue);
            Assert.Equal("false", log.Properties.Single(x => x.Name == "Enabled").EncodedValue);
            var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump); Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            await table.UpsertEntityAsync(new TableEntity("ALog", "SourceJob_12") { ["Message"] = "Updated log" }, TableUpdateMode.Replace);
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true, includeTables: true); await new CoreDatasetImporter(database, options).ImportAsync(_dump);
            Assert.Equal(id, (await database.ArchivedOperationalLogs.SingleAsync()).Id); Assert.Single((await database.ArchivedOperationalLogs.SingleAsync()).Properties);
            await table.DeleteEntityAsync("ALog", "SourceJob_12");
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true, includeTables: true); await new CoreDatasetImporter(database, options).ImportAsync(_dump);
            Assert.Empty(await database.ArchivedOperationalLogs.ToArrayAsync());
        }
        finally { await table.DeleteAsync(); }
        async Task<bool> TableExistsAsync() { await foreach (var item in _storage.Tables.QueryAsync(filter: "TableName eq 'logs'")) return true; return false; }
    }

    [CoreFact]
    public async Task Historical_logs_are_excluded_by_default_preserved_in_dump_and_can_be_opted_in_without_hiding_unknown_tables()
    {
        var logs = _storage.Tables.GetTableClient("logs");
        var unknownName = "unmapped" + Guid.NewGuid().ToString("N");
        var unknown = _storage.Tables.GetTableClient(unknownName);
        await logs.CreateAsync();
        var ownsUnknown = false;
        try
        {
            await unknown.CreateAsync(); ownsUnknown = true;
            await logs.AddEntityAsync(new TableEntity("ALog", "old-job") { ["Message"] = "Original historical log" });
            await unknown.AddEntityAsync(new TableEntity("future", "data") { ["Message"] = "Unmapped data must remain pending" });
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true, includeTables: true);

            var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
            Assert.DoesNotContain(dataset.Documents, x => x.Container == "table:logs");
            Assert.Empty(plan.Logs);
            await using var database = fixture.CreateContext();
            var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Contains("table:logs", report.ExcludedContainers!);
            Assert.DoesNotContain("table:logs", report.PendingContainers);
            Assert.Contains("table:" + unknownName, report.PendingContainers);
            Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("table:logs:"));
            Assert.Empty(await database.ArchivedOperationalLogs.ToArrayAsync());
            Assert.Equal(2, await database.Groups.CountAsync());
            var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(repeated.Documents, repeated.SkippedDocuments);

            var included = await new CoreDatasetImporter(database, new(ImportLegacyLogs: true)).ImportAsync(_dump);
            Assert.DoesNotContain("table:logs", included.ExcludedContainers!);
            Assert.Equal("Original historical log", (await database.ArchivedOperationalLogs.SingleAsync()).Message);
            // Re-excluding must not hit the unchanged-document fast path and
            // leave an archive behind. Interactive-write protection is unchanged.
            var excludedAgain = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(0, excludedAgain.SkippedDocuments);
            Assert.Empty(await database.ArchivedOperationalLogs.ToArrayAsync());
            Assert.Equal(2, await database.Groups.CountAsync());
            var manifest = await DumpArchive.VerifyAsync(_dump);
            Assert.Equal(1, manifest.Tables.Single(x => x.Name == "logs").EntityCount);
            Assert.Equal("Original historical log", (await logs.GetEntityAsync<TableEntity>("ALog", "old-job")).Value["Message"]);
        }
        finally
        {
            if (ownsUnknown) await unknown.DeleteAsync();
            await logs.DeleteAsync();
        }
    }
}
