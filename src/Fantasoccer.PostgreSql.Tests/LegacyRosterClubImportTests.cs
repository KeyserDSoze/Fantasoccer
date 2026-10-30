using System.Text.Json;
using System.Text.Json.Nodes;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    // Exact 2012/13-2021/22 roster shape: the day's club was never recorded.
    private const string EmptyClubRosterEntry = """{"p":12,"s":0,"k":3,"n":"Old forward","t":{"n":""},"r":3,"a":true}""";

    [CoreFact]
    public async Task Legacy_rosters_and_hall_snapshots_without_a_club_keep_NULL_and_their_money_without_inventing_a_club()
    {
        await PutRosterWithRawEntryAsync(EmptyClubRosterEntry);
        var daily = new Team { Name = "Team", Owner = Email, AdditionalOwners = [OtherEmail], Players = [] };
        var dailyEnvelope = JsonNode.Parse(JsonSerializer.Serialize(LegacyEntity<Team, TeamDayKey>.Default(daily,
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Day = 3, Email = Email })))!;
        dailyEnvelope["v"]!["p"]!.AsArray().Add(JsonNode.Parse(EmptyClubRosterEntry));
        await (await ExtraContainerAsync("dailyteams")).GetBlobClient("first-day").UploadAsync(BinaryData.FromString(dailyEnvelope.ToJsonString()));
        var hallPlayer = new Player { Name = "Old forward", RealTeam = new() { Name = "" }, Role = Role.Forward, Price = 12, Position = FantaSoccerRole.Forward, IsActive = true };
        await (await ExtraContainerAsync("halloffame")).GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            LegacyEntity<HallOfFame, GeneralLeagueKey>.Default(new() { RecordPlayer = new() { Player = hallPlayer, Points = 18.5f } }, new() { Group = "first", League = "league" }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);

        var rosters = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).Where(x => x.SeasonIndex == 15).ToArrayAsync();
        var season = rosters.Single(x => x.Kind == RosterKind.Season).Players.OrderBy(x => x.Ordinal).ToArray();
        Assert.Equal(("clb", "oldforward", (string?)null, 12, 0), (season[0].ClubId, season[1].PlayerId, season[1].ClubId, season[1].Price, season[1].Status));
        var day = Assert.Single(rosters.Single(x => x.Kind == RosterKind.Matchday).Players);
        Assert.Null(day.ClubId); Assert.Equal(12, day.Price);
        Assert.Null((await database.HallArchives.AsSplitQuery().SingleAsync()).PlayerRecords.Single().Player.ClubId);
        // The annual player row keeps its own evidence; the roster never invents one.
        Assert.Null((await database.PlayerSeasons.SingleAsync(x => x.PlayerId == "oldforward")).ClubId);

        var missing = report.MissingRosterClubSnapshots!.OrderBy(x => x.Container).ToArray();
        Assert.Equal(new[] { ("dailyteams", 15, 3, "$.v.p[0].t"), ("halloffame", 0, 0, "$.v.rp.p.t"), ("team", 15, 0, "$.v.p[1].t") },
            missing.Select(x => (x.Container, x.SeasonIndex, x.Day, x.FirstJsonPath)));
        Assert.All(missing, x => Assert.Equal(1, x.Records));
        Assert.DoesNotContain("Old forward", JsonSerializer.Serialize(report));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .MigrateAsync("20261028112843_PostponedMatchPoliciesAndLegacyVotes"));
        Assert.Contains("Cannot downgrade legacy roster or hall snapshots", refused.MessageText);
        Assert.Null((await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).SingleAsync(x => x.Kind == RosterKind.Matchday)).Players.Single().ClubId);
    }

    [CoreFact]
    public async Task A_roster_club_with_an_unverified_name_and_no_code_still_refuses_the_import()
    {
        await PutRosterWithRawEntryAsync(EmptyClubRosterEntry.Replace("\"t\":{\"n\":\"\"}", "\"t\":{\"n\":\"Unknown fixture club\"}", StringComparison.Ordinal));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_club_code", fault.Message);
        Assert.Equal("$.v.p[1].t", fault.ClubValidation!.JsonPath);
    }
}
