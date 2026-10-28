using System.Text.Json;
using System.Text.Json.Nodes;
using Fantasoccer.DataMigration;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    // Exact 2021/22 shape: sold for half price, no name, no club, role -1.
    private const string NamelessSoldRosterEntry = """{"p":16,"s":1,"k":11,"n":"","t":{"n":""},"r":-1,"a":true}""";

    private async Task PutRosterWithRawEntryAsync(string rawEntry)
    {
        var team = new Team { Name = "Team", Owner = Email, AdditionalOwners = [OtherEmail], Players = [new() { Name = "Roster forward",
            RealTeam = new() { Name = "Club", Abbreviation = "clb" }, Role = Role.Forward, IsActive = true, Visible = true,
            Status = PlayerInTeamStatus.Active, Price = 30, Position = FantaSoccerRole.Forward }] };
        var envelope = JsonNode.Parse(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(team,
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email })))!;
        envelope["v"]!["p"]!.AsArray().Add(JsonNode.Parse(rawEntry));
        var container = _extraContainers.Contains("team") ? _storage.Blobs.GetBlobContainerClient("team") : await ExtraContainerAsync("team");
        await container.GetBlobClient("first").UploadAsync(BinaryData.FromString(envelope.ToJsonString()), overwrite: true);
    }

    [CoreFact]
    public async Task Rows_without_a_usable_name_are_excluded_from_lineups_statistics_and_sold_roster_entries_with_their_money_reported()
    {
        var club = new RealTeam { Name = "Club", Abbreviation = "clb" };
        ChancedRealPlayer Chance(string name) => new() { Name = name, RealTeam = club, Role = Role.Forward, IsActive = true, Visible = true, Chance = new() { Gazzetta = true } };
        var chances = await ExtraContainerAsync("chancedrealplayerwrapper");
        await chances.GetBlobClient("lineups").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<ChancedRealPlayerWrapper, YearDayKey>.Default(
            new() { Players = [Chance("Chance forward"), Chance("----"), Chance("")] }, new() { Year = 15, Day = 3 }))));
        var statistics = await ExtraContainerAsync("statplayerswrapper");
        await statistics.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<StatPlayersWrapper, int>.Default(
            new() { Players = [new() { Name = "", RealTeam = club, Role = Role.Forward }, new() { Name = "Stat forward", RealTeam = club, Role = Role.Forward, Summatory = 6.5f }] }, 15))));
        await PutRosterWithRawEntryAsync(NamelessSoldRosterEntry);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);

        // Probable lineups are parsed and validated (cutover cache seed) but not stored in SQL.
        Assert.Equal(new[] { "chanceforward" }, (await CoreDatasetImporter.AnalyzeAsync(_dump)).Plan.Chances.Where(x => x.SeasonIndex == 15 && x.Day == 3).Select(x => x.PlayerId).ToArray());
        Assert.False(await database.FootballChances.AnyAsync());
        Assert.Equal(new[] { "statforward" }, await database.FootballPlayerStatistics.Where(x => x.SeasonIndex == 15).Select(x => x.PlayerId).ToArrayAsync());
        Assert.False(await database.Players.AnyAsync(x => x.Name == "" || x.Name == "----"));
        var skipped = report.SkippedUnusablePlayerRows!.OrderBy(x => x.Container).ToArray();
        Assert.Equal(("chancedrealplayerwrapper", 15, 3, 3, 2, "$.v.p[1]"), (skipped[0].Container, skipped[0].SeasonIndex, skipped[0].Day, skipped[0].TotalEntries, skipped[0].Records, skipped[0].FirstJsonPath));
        Assert.Equal(("statplayerswrapper", 15, 0, 2, 1, "$.v.p[0]"), (skipped[1].Container, skipped[1].SeasonIndex, skipped[1].Day, skipped[1].TotalEntries, skipped[1].Records, skipped[1].FirstJsonPath));

        var roster = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).SingleAsync(x => x.Kind == RosterKind.Season && x.SeasonIndex == 15);
        var kept = Assert.Single(roster.Players);
        Assert.Equal(("rosterforward", 0, 30), (kept.PlayerId, kept.Ordinal, kept.Price));
        var excluded = Assert.Single(report.ExcludedNamelessRosterEntries!);
        Assert.Equal(("team", "$.v.p[1]", 15, 0), (excluded.Container, excluded.JsonPath, excluded.SeasonIndex, excluded.Day));
        Assert.Equal((16, 0, 1, 11), (excluded.Price, excluded.Revenue, excluded.Status, excluded.Position));
        Assert.Equal(manifest.Containers.Single(x => x.Name == "team").Blobs.Single().Sha256, excluded.DocumentSha256);
        Assert.DoesNotContain("Roster forward", JsonSerializer.Serialize(report));

        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task An_active_roster_entry_without_a_name_still_refuses_the_import()
    {
        await PutRosterWithRawEntryAsync(NamelessSoldRosterEntry.Replace("\"s\":1", "\"s\":0", StringComparison.Ordinal));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_player", fault.Message);
        Assert.Equal("$.v.p[1]", fault.PlayerValidation!.JsonPath);
        Assert.Contains("empty_player_key", fault.PlayerValidation.Reasons);
    }
}
