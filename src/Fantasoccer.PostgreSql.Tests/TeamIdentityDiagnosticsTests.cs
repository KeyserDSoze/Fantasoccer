using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Unresolved_historical_team_identities_report_container_path_and_document_without_the_owner()
    {
        // OtherEmail is only a co-owner of the 2026/27 team: it has no annual team of its own.
        var rosters = await ExtraContainerAsync("team");
        Team Roster(string owner) => new() { Name = "Team", Owner = owner, AdditionalOwners = [], Players = [] };
        async Task PutRosterAsync(string owner) => await rosters.GetBlobClient("roster").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            LegacyEntity<Team, TeamKey>.Default(Roster(owner), new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = owner }))), overwrite: true);
        await PutRosterAsync(OtherEmail);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var roster = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("unresolved_historical_team_identity", roster.Message);
        Assert.Equal("team $.Key.e", roster.Field);
        Assert.StartsWith("RosterImport.cs:", roster.Reader);
        Assert.Equal(manifest.Containers.Single(x => x.Name == "team").Blobs.Single().Sha256, roster.DocumentSha256);
        Assert.DoesNotContain(OtherEmail, CoreImportDiagnostics.Format(roster), StringComparison.OrdinalIgnoreCase);

        await PutRosterAsync(Email);
        var ranks = await ExtraContainerAsync("rank");
        await ranks.GetBlobClient("rank").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Rank, LeagueKey>.Default(
            new() { SerieADay = 5, RankedTeams = new() { ["Stage"] = [new() { Name = "Team", Owner = Email, Point = 7 }, new() { Name = "Co-owner", Owner = OtherEmail, Point = 3 }] } },
            new() { Group = "first", League = "league", Year = 15 }))));
        manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var rank = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("unresolved_historical_team_identity", rank.Message);
        Assert.Equal("rank $.Value.r.Stage[1].o", rank.Field);
        Assert.StartsWith("FantasyCompetitionImport.cs:", rank.Reader);
        Assert.Equal(manifest.Containers.Single(x => x.Name == "rank").Blobs.Single().Sha256, rank.DocumentSha256);
        Assert.DoesNotContain(OtherEmail, CoreImportDiagnostics.Format(rank), StringComparison.OrdinalIgnoreCase);
    }
}
