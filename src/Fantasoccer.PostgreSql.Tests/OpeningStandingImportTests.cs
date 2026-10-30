using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Opening_standings_keep_saved_scores_prizes_and_previous_year_team_without_recalculation()
    {
        _first.Baskets[0].Years.Add(new() { Year = 14, Teams = [new() { Name = "Previous team", Owner = Email, AdditionalOwners = [] }] });
        await PutGroupAsync(_first);
        var container = await ExtraContainerAsync("openingstandings");
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<OpeningCompetitionStandings, OpeningCompetitionKey>.Default(
            new() { Group = "first", League = "league", Year = 15, SerieADays = 2, Teams =
                [new() { Owner = Email, Name = "Saved opening name", Score = 154.75f, Prize = 75, Position = 1 }] },
            new() { Group = "first", League = "league", Year = 15 }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var snapshot = await database.OpeningStandingSnapshots.SingleAsync();
        var row = Assert.Single(snapshot.Teams);
        Assert.Equal(14, row.TeamOriginYear);
        Assert.Equal(154.75f, row.Score);
        Assert.Equal(75, row.Prize);
        Assert.Equal("Saved opening name", row.Name);
        Assert.Equal(row.TeamId, (await database.TeamSeasons.SingleAsync(x => x.Id == row.TeamId)).Id);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await container.DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.OpeningStandingSnapshots.ToArrayAsync());
    }
}
