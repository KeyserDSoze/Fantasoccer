using System.Security.Cryptography;
using System.Text;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Current_only_rank_upgrade_refuses_existing_daily_values_without_deleting_them_and_preserves_current_financial_values()
    {
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261027182301_OptionalHistoricalVoteClubs");
        var group = await database.Groups.SingleAsync(x => x.Name == "first");
        // Old schema: project only columns that existed then.
        var league = await database.Leagues.Where(x => x.GroupId == group.Id && x.Name == "League").Select(x => new { x.LeagueId }).SingleAsync();
        var team = await database.TeamSeasons.SingleAsync(x => x.GroupId == group.Id && x.SeasonIndex == 15);
        foreach (var day in new[] { 0, 3 })
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.Id, LeagueId = league.LeagueId, SeasonIndex = 15, SnapshotDay = day, SerieADay = 3,
                Stages = [new() { Stage = "@", Teams = [new() { TeamId = team.Id, BasketId = team.BasketId, Name = "Original rank",
                    Owner = Email, Points = 7.25f, ValuePoints = 711.5f, Money = 123, PlusMoney = 8 }] }] });
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();
        var fault = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("Daily fantasy standings already exist", fault.MessageText);
        Assert.Contains("No standings or financial values were deleted", fault.MessageText);
        var rows = await database.FantasyStandingSnapshots.AsNoTracking().AsSplitQuery().ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, x => { var row = Assert.Single(Assert.Single(x.Stages).Teams); Assert.Equal(123, row.Money); Assert.Equal(8, row.PlusMoney); Assert.Equal(7.25f, row.Points); });
        // Remove only this test's known synthetic daily copy, after proving that
        // the upgrade refused to discard it. No operator data is touched.
        await database.FantasyStandingSnapshots.Where(x => x.GroupId == group.Id && x.SnapshotDay == 3).ExecuteDeleteAsync();
        await migrator.MigrateAsync();
        var current = await database.FantasyStandingSnapshots.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.Equal(0, current.SnapshotDay); Assert.Equal(3, current.SerieADay);
        var saved = Assert.Single(Assert.Single(current.Stages).Teams);
        Assert.Equal(123, saved.Money); Assert.Equal(8, saved.PlusMoney); Assert.Equal(711.5f, saved.ValuePoints);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync("UPDATE \"FantasyStandingSnapshots\" SET \"SnapshotDay\"=1"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [CoreFact]
    public async Task Legacy_daily_rank_blobs_are_verified_but_not_decoded_or_imported_and_cannot_manufacture_a_current_rank()
    {
        var container = await ExtraContainerAsync("dailyrank");
        var blob = container.GetBlobClient("private-daily-owner@example.test");
        const string original = "not-json-private-daily-snapshot";
        await blob.UploadAsync(BinaryData.FromString(original)); var etag = (await blob.GetPropertiesAsync()).Value.ETag;
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        Assert.DoesNotContain(dataset.Documents, x => x.Container == "dailyrank"); Assert.Empty(plan.Standings);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.FantasyStandingSnapshots.ToArrayAsync()); Assert.Contains("dailyrank", report.ExcludedContainers!);
        Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("dailyrank:", StringComparison.Ordinal));
        Assert.DoesNotContain("private-daily", System.Text.Json.JsonSerializer.Serialize(report));
        Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag); Assert.Equal(original, (await blob.DownloadContentAsync()).Value.Content.ToString());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("dailyrank\0" + blob.Name)));
        var file = Path.Combine(_dump, "blobs", hash + ".bin");
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        await File.AppendAllTextAsync(file, "tampered synthetic bytes");
        await Assert.ThrowsAsync<InvalidDataException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
    }
}
