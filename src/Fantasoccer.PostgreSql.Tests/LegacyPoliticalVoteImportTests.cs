using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    private static string LegacyOfficialRow(string name, string club, float value = 6, int suffered = 0, bool entered = true, int role = 0) =>
        "{\"v\":{\"r\":" + role + ",\"v\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"i\":true,\"g\":0,\"p\":0,\"a\":0,\"s\":0,\"d\":" + suffered +
        ",\"w\":0,\"o\":0,\"t\":0,\"h\":true,\"u\":false,\"n\":" + (entered ? "true" : "false") + "},\"n\":\"" + name + "\",\"t\":{\"n\":\"" + club + "\",\"a\":null},\"r\":" + role + ",\"a\":true}";

    private async Task PutLegacyCalendarAsync(int year, params (int Day, string Home, string Away, bool Delayed)[] games)
    {
        var calendar = new RealCalendar { Year = year, Days = games.GroupBy(x => x.Day).Select(day => new RealDay { Year = year, SerieADay = day.Key,
            Games = day.Select(x => new RealGame { Home = new() { Name = x.Home }, Away = new() { Name = x.Away }, Delayed = x.Delayed,
                Date = new DateTime(2022, 3, 1, 15, 0, 0), HomeGoal = x.Delayed ? null : 1, AwayGoal = x.Delayed ? null : 0 }).ToList() }).ToList() };
        var container = _extraContainers.Contains("realcalendar") ? _storage.Blobs.GetBlobContainerClient("realcalendar") : await ExtraContainerAsync("realcalendar");
        await container.GetBlobClient(year.ToString()).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<RealCalendar, int>.Default(calendar, year))), overwrite: true);
    }

    [CoreFact]
    public async Task Legacy_political_six_beside_an_actual_vote_is_preserved_apart_and_attests_the_postponed_game_for_both_clubs()
    {
        // Reproduces the real shape: same name/club/role, actual vote with a
        // conceded goal and isIn:true, plus the legacy delayed 6 with isIn:false.
        await PutLegacyCalendarAsync(10, (26, "cag", "Napoli", false), (26, "Milan", "Inter", false));
        var political = LegacyOfficialRow("Fixture keeper", "cag", entered: false);
        var rows = string.Join(",", LegacyOfficialRow("Fixture keeper", "cag", suffered: 1), LegacyOfficialRow("Napoli player", "Napoli", 6.5f),
            LegacyOfficialRow("Bench keeper", "cag", entered: false), LegacyOfficialRow("Napoli bench", "Napoli", entered: false),
            LegacyOfficialRow("Genuine milan", "Milan", entered: false), LegacyOfficialRow("Inter player", "Inter", 7), political);
        var official = await ExtraContainerAsync("official");
        var json = LegacyVoteFixtureEnvelope("official", 10, rows, historical: true).Replace("\"d\":1}", "\"d\":26}", StringComparison.Ordinal);
        var blob = official.GetBlobClient("legacy-delayed-day");
        await blob.UploadAsync(BinaryData.FromString(json)); var etag = (await blob.GetPropertiesAsync()).Value.ETag;
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);

        var votes = await database.FootballVotes.AsNoTracking().Include(x => x.Player).Where(x => x.SeasonIndex == 10 && x.Day == 26).ToArrayAsync();
        Assert.Equal(new[] { "Fixture keeper", "Genuine milan", "Inter player", "Napoli player" }, votes.Select(x => x.Player.Name).Order());
        var actual = votes.Single(x => x.Player.Name == "Fixture keeper");
        Assert.Equal(6, actual.Value); Assert.Equal(1, actual.SufferedGoals); Assert.True(actual.IsIn); Assert.Equal("cag", actual.ClubId);
        // Without calendar or paired evidence, a 6 with isIn:false stays an actual vote.
        Assert.False(votes.Single(x => x.Player.Name == "Genuine milan").IsIn);

        var legacy = await database.LegacyPoliticalVoteEntries.AsNoTracking().OrderBy(x => x.SourceOrdinal).ToArrayAsync();
        Assert.Equal(new[] { 2, 3, 6 }, legacy.Select(x => x.SourceOrdinal));
        Assert.Equal(new[] { "cag", "nap", "cag" }, legacy.Select(x => x.ClubId));
        Assert.Equal(political, legacy[2].OriginalPayloadJson); Assert.Equal("$.Value.p[6]", legacy[2].JsonPath);
        Assert.All(legacy, x => Assert.Equal(26, x.Day));
        Assert.Equal(KeyUtils.GetPlayerKey("Fixture keeper"), legacy[2].PlayerId);
        var summary = Assert.Single(report.LegacyPoliticalVotes!);
        Assert.Equal(3, summary.Records); Assert.Equal(1, summary.WithActualCounterpart); Assert.Equal("$.Value.p[2]", summary.FirstJsonPath);
        Assert.DoesNotContain("Fixture keeper", JsonSerializer.Serialize(report));

        var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == 10 && x.SerieADay == 26).OrderBy(x => x.Ordinal).ToArrayAsync();
        Assert.True(matches[0].WasPostponed); Assert.False(matches[0].Delayed); Assert.True(matches[0].ActualOfficialVotesReady);
        Assert.False(matches[1].WasPostponed); Assert.Null(matches[1].ActualOfficialVotesReady);
        Assert.NotNull(matches[0].OriginalKickoffUtc);

        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Equal(3, await database.LegacyPoliticalVoteEntries.CountAsync());
        Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag); Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
    }

    [CoreFact]
    public async Task Inactive_six_substitutes_are_removed_without_creating_players_while_actual_votes_and_active_sixes_stay()
    {
        // Real 2024/25 day 14 shape: the substitute copies only name and vote
        // (no club, player role 0, IsActive:false) next to the actual vote.
        const string actual = """{"v":{"r":3,"v":6.5,"i":true,"g":0,"p":0,"a":1,"s":0,"d":0,"w":0,"o":0,"t":0,"c":false,"h":true,"u":false,"n":true,"j":false},"n":"Fixture forward","t":{"n":"Lecce","a":"lec"},"r":3,"a":true,"vh":true}""";
        const string substitute = """{"v":{"r":3,"v":6,"i":true,"g":0,"p":0,"a":0,"s":0,"d":0,"w":0,"o":0,"t":0,"c":false,"h":true,"u":false,"n":false,"j":false},"n":"Fixture forward","t":null,"r":0,"a":false,"vh":true}""";
        var unlisted = substitute.Replace("Fixture forward", "Unlisted bench", StringComparison.Ordinal).Replace("\"r\":3", "\"r\":1", StringComparison.Ordinal);
        var activeSix = LegacyOfficialRow("Active six", "Milan", entered: false);
        var official = await ExtraContainerAsync("official");
        var json = LegacyVoteFixtureEnvelope("official", 13, string.Join(",", actual, substitute, unlisted, activeSix), historical: true)
            .Replace("\"d\":1}", "\"d\":14}", StringComparison.Ordinal);
        var blob = official.GetBlobClient("inactive-substitutes"); await blob.UploadAsync(BinaryData.FromString(json));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);

        var votes = await database.FootballVotes.AsNoTracking().Include(x => x.Player).Where(x => x.SeasonIndex == 13 && x.Day == 14).OrderBy(x => x.Player.Name).ToArrayAsync();
        Assert.Equal(new[] { "Active six", "Fixture forward" }, votes.Select(x => x.Player.Name));
        var kept = votes.Single(x => x.Player.Name == "Fixture forward");
        Assert.Equal((6.5f, 1, "lec", 3, true), (kept.Value, kept.Assists, kept.ClubId, kept.PlayerRole, kept.IsIn));
        Assert.False(votes.Single(x => x.Player.Name == "Active six").IsIn);
        Assert.False(await database.Players.AnyAsync(x => x.Name == "Unlisted bench"));
        var annual = await database.PlayerSeasons.SingleAsync(x => x.PlayerId == KeyUtils.GetPlayerKey("Fixture forward") && x.SeasonIndex == 13);
        Assert.Equal((PlayerPosition.Forward, "lec"), (annual.Position, annual.ClubId));
        Assert.Empty(await database.LegacyPoliticalVoteEntries.ToArrayAsync());

        var skipped = Assert.Single(report.SkippedInactiveSubstitutes!);
        Assert.Equal((13, 14, 2, 1, "$.Value.p[1]"), (skipped.SeasonIndex, skipped.Day, skipped.Records, skipped.WithActualCounterpart, skipped.FirstJsonPath));
        Assert.DoesNotContain("Fixture forward", JsonSerializer.Serialize(report)); Assert.DoesNotContain("Unlisted bench", JsonSerializer.Serialize(report));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
    }

    [CoreFact]
    public async Task Calendar_attested_substitutes_are_kept_out_of_actual_votes_but_two_actual_votes_still_refuse_the_import()
    {
        await PutLegacyCalendarAsync(10, (27, "cag", "Napoli", true));
        var official = await ExtraContainerAsync("official");
        var substitute = official.GetBlobClient("legacy-delayed-only");
        await substitute.UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("official", 10, LegacyOfficialRow("Waiting keeper", "cag", entered: false), historical: true)
            .Replace("\"d\":1}", "\"d\":27}", StringComparison.Ordinal)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using (var database = fixture.CreateContext())
        {
            await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Empty(await database.FootballVotes.Where(x => x.SeasonIndex == 10 && x.Day == 27).ToArrayAsync());
            Assert.Equal("cag", (await database.LegacyPoliticalVoteEntries.SingleAsync()).ClubId);
            var match = await database.FootballMatches.SingleAsync(x => x.SeasonIndex == 10 && x.SerieADay == 27);
            Assert.True(match.Delayed); Assert.True(match.WasPostponed); Assert.False(match.ActualOfficialVotesReady);
        }
        var conflicting = official.GetBlobClient("two-actual-votes");
        await conflicting.UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("official", 10,
            LegacyOfficialRow("Played twice", "Milan", suffered: 1) + "," + LegacyOfficialRow("Played twice", "Milan", 6.5f), historical: true)
            .Replace("\"d\":1}", "\"d\":28}", StringComparison.Ordinal)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("duplicate_or_invalid_vote", fault.Message);
        Assert.Contains("duplicate_player_vote", fault.VoteValidation!.Reasons);
    }
}
