using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Rosters_under_a_basket_without_the_owners_team_are_skipped_only_as_identical_copies_or_empty_orphans()
    {
        const string left = "left@example.test";
        _first.Baskets.Add(new() { Id = "other-basket", Name = "Other", Years = [new() { Year = 15, Teams = [new() { Name = "Third", Owner = "third@example.test", AdditionalOwners = [] }] }] });
        _first.Baskets[0].Years.Add(new() { Year = 14, Teams = [new() { Name = "Left team", Owner = left, AdditionalOwners = [] }] });
        await PutGroupAsync(_first);
        var rosters = await ExtraContainerAsync("team");
        async Task PutAsync(string blob, string basket, string owner, Team team) => await rosters.GetBlobClient(blob).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            Entity<Team, TeamKey>.Default(team, new() { Group = "first", BasketId = basket, Year = 15, Email = owner }))), overwrite: true);
        Team Roster(int money) => new() { Name = "Team", Owner = Email, AdditionalOwners = [], MoneyFromRank = money, FormationChanges = 2,
            Players = [new() { Name = "Copied fixture player", Role = Role.Forward, RealTeam = new() { Name = "Napoli", Abbreviation = "nap" }, Price = 12, Revenue = 0 }] };
        Team Empty(int money) => new() { Name = "Left team", Owner = left, AdditionalOwners = [], Players = [], MoneyFromRank = money };
        await PutAsync("kept", "same-local-basket", Email, Roster(17));

        // A copy that differs, even only in money, and an orphan that carries money still stop the import.
        await PutAsync("copy", "other-basket", Email, Roster(18));
        await PutAsync("empty", "same-local-basket", left, Empty(0));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var different = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal(("unresolved_historical_team_identity", "team $.Key.e"), (different.Message, different.Field));
        await PutAsync("copy", "other-basket", Email, Roster(17));
        await PutAsync("empty", "same-local-basket", left, Empty(5));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var orphan = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal(("unresolved_historical_team_identity", "team $.Key.e"), (orphan.Message, orphan.Field));

        await PutAsync("empty", "same-local-basket", left, Empty(0));
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        string Sha(string blob) => manifest.Containers.Single(x => x.Name == "team").Blobs.Single(x => x.Name == blob).Sha256;
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(43, report.MappingVersion);
        var skipped = report.SkippedRosterDocuments!.OrderBy(x => x.Reason).ToArray();
        Assert.Equal(2, skipped.Length);
        Assert.Equal(("empty_roster_without_team", Sha("empty"), "team", 15, 0, (string?)null), (skipped[0].Reason, skipped[0].DocumentSha256, skipped[0].Container, skipped[0].SeasonIndex, skipped[0].Day, skipped[0].KeptDocumentSha256));
        Assert.Equal(("identical_copy_in_team_basket", Sha("copy"), Sha("kept")), (skipped[1].Reason, skipped[1].DocumentSha256, skipped[1].KeptDocumentSha256));
        Assert.StartsWith("blobs/", skipped[1].KeptArchiveReference);
        Assert.Contains(report.PendingFields, x => x.Contains("skippedRosterDocuments", StringComparison.Ordinal));
        Assert.DoesNotContain(Email, JsonSerializer.Serialize(report.SkippedRosterDocuments), StringComparison.OrdinalIgnoreCase);
        var roster = Assert.Single(await database.RosterSnapshots.Include(x => x.Players).ToArrayAsync());
        Assert.Equal((17, 2, 12), (roster.MoneyFromRank, roster.FormationChanges, roster.Players.Single().Price));
        var team = await database.TeamSeasons.SingleAsync(x => x.Id == roster.TeamId);
        Assert.Equal(team.BasketId, roster.BasketId);
        Assert.Equal("Basket", (await database.Baskets.SingleAsync(x => x.BasketId == roster.BasketId)).Name);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        Assert.Single(await database.RosterSnapshots.ToArrayAsync());
    }
}
