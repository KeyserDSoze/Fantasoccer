using System.Text.Json;
using System.Text.Json.Nodes;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Club_import_does_not_turn_the_legacy_name_prefix_fallback_into_a_natural_key()
    {
        var container = await ExtraContainerAsync("realteamwrapper");
        var blob = container.GetBlobClient("15");
        await blob.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Napoli\"}]}}"));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_club_code", fault.Message);
        Assert.DoesNotContain("Napoli", CoreImportDiagnostics.Format(fault));
        await blob.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Napoli\",\"a\":\"NAP\"}]}}"), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (_, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        Assert.Equal("nap", Assert.Single(plan.Clubs).Id);
        var ranks = await ExtraContainerAsync("realrank");
        await ranks.GetBlobClient("15").UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"r\":[{\"n\":\"Napoli\",\"p\":4.5}]}}"));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (_, linked) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        Assert.Single(linked.Clubs); Assert.Equal("nap", Assert.Single(linked.ClubRanks).ClubId);
        await blob.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Napoli\",\"a\":\"NAP\"},{\"n\":\"Napoli\",\"a\":\"XYZ\"}]}}"), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        Assert.Equal("ambiguous_real_rank_club", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump))).Message);
    }

    [CoreFact]
    public async Task Roster_only_players_create_global_and_year_rows_once_using_natural_keys_without_requiring_a_catalogue_entry()
    {
        var catalogues = await ExtraContainerAsync("realplayerswrapper");
        await catalogues.GetBlobClient("15").UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"p\":[{\"n\":\"\",\"t\":{\"n\":\"rec\"},\"r\":3,\"a\":true}]}}"));
        var container = await ExtraContainerAsync("team");
        var source = new Team { Name = "Historical roster", Owner = Email, Players = [new() { Name = "Roster only", RealTeam = new() { Name = "Recovery club", Abbreviation = "rec" },
            Role = Role.Forward, Price = 53, Revenue = 70, Status = PlayerInTeamStatus.Sold, Position = FantaSoccerRole.Tribune }] };
        foreach (var group in new[] { "first", "second" })
        {
            var json = JsonNode.Parse(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(source,
                new() { Group = group, BasketId = "same-local-basket", Year = 15, Email = Email })))!;
            json["v"]!["p"]![0]!["PlayerKey"] = "rosteronly";
            await container.GetBlobClient(group).UploadAsync(BinaryData.FromString(json.ToJsonString()));
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        var person = await database.Players.SingleAsync(); Assert.Equal("rosteronly", person.Id); Assert.Equal("Roster only", person.Name);
        var annual = await database.PlayerSeasons.SingleAsync(); Assert.Equal("rosteronly", annual.PlayerId); Assert.Equal(15, annual.SeasonIndex);
        Assert.Equal("rec", annual.ClubId); Assert.Equal("rec", (await database.Clubs.SingleAsync()).Id);
        Assert.Equal(1, report.UnresolvedPlayerCatalogEntries); Assert.Single(report.RecoveredRosterPlayers!);
        Assert.True(report.RecoveredRosterPlayers![0].CreatedGlobalPlayer);
        var rosters = await database.RosterSnapshots.AsNoTracking().AsSplitQuery().ToArrayAsync(); Assert.Equal(2, rosters.Length);
        foreach (var roster in rosters)
        {
            var row = Assert.Single(roster.Players); Assert.Equal("rosteronly", row.PlayerId); Assert.Equal("rec", row.ClubId);
            Assert.Equal(53, row.Price); Assert.Equal(70, row.Revenue); Assert.Equal((int)PlayerInTeamStatus.Sold, row.Status);
        }
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Equal(1, await database.Players.CountAsync());
    }

    [CoreFact]
    public async Task A_known_explicit_roster_key_can_link_history_across_years_but_a_key_name_conflict_never_creates_a_new_identity()
    {
        var catalogues = await ExtraContainerAsync("realplayerswrapper");
        await catalogues.GetBlobClient("14").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealPlayersWrapper, int>.Default(
            new() { Players = [new() { Name = "Known player", RealTeam = new() { Name = "Recovery club", Abbreviation = "rec" }, Role = Role.Forward }] }, 14))));
        var container = await ExtraContainerAsync("team");
        var team = new Team { Name = "Historical roster", Owner = Email, Players = [new() { Name = "", RealTeam = new() { Name = "Recovery club", Abbreviation = "rec" },
            Role = Role.Forward, Price = 41, Position = FantaSoccerRole.Forward }] };
        var json = JsonNode.Parse(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(team,
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email })))!;
        json["v"]!["p"]![0]!["Key"] = "knownplayer";
        var blob = container.GetBlobClient("first"); await blob.UploadAsync(BinaryData.FromString(json.ToJsonString()));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(1, await database.Players.CountAsync()); Assert.Equal(new[] { 14, 15 }, await database.PlayerSeasons.OrderBy(x => x.SeasonIndex).Select(x => x.SeasonIndex).ToArrayAsync());
        Assert.False(Assert.Single(report.RecoveredRosterPlayers!).CreatedGlobalPlayer);
        Assert.Equal("knownplayer", (await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync()).Players.Single().PlayerId);
        json["v"]!["p"]![0]!["n"] = "Different name";
        await blob.UploadAsync(BinaryData.FromString(json.ToJsonString()), overwrite: true); await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("player_identity_conflict", fault.Message); Assert.Contains("player_key_name_mismatch", fault.PlayerValidation!.Reasons);
        Assert.DoesNotContain("Different name", CoreImportDiagnostics.Format(fault));
        Assert.Equal(1, await database.Players.CountAsync()); Assert.Equal(2, await database.PlayerSeasons.CountAsync());
        foreach (var invalid in new JsonNode?[] { JsonValue.Create(7), JsonValue.Create("bad-key"), JsonValue.Create("differentkey") })
        {
            json["v"]!["p"]![0]!["n"] = "Known player";
            json["v"]!["p"]![0]!["PlayerKey"] = invalid;
            await blob.UploadAsync(BinaryData.FromString(json.ToJsonString()), overwrite: true);
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var invalidKey = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
            Assert.Equal("player_identity_conflict", invalidKey.Message);
            Assert.Contains("invalid_stored_player_key", invalidKey.PlayerValidation!.Reasons);
            Assert.Equal(1, await database.Players.CountAsync()); Assert.Equal(2, await database.PlayerSeasons.CountAsync());
        }
    }
}
