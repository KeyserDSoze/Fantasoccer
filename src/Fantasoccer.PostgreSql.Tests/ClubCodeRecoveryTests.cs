using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Legacy_name_only_club_catalogues_use_verified_provider_codes_in_both_envelopes_without_rewriting_originals()
    {
        var container = await ExtraContainerAsync("realteamwrapper");
        var blob = container.GetBlobClient("verified-catalogue");
        var names = FantacalcioClubCodes.Names.Keys.Order(StringComparer.Ordinal).ToArray();
        await using var database = fixture.CreateContext();
        foreach (var oldEnvelope in new[] { true, false })
        {
            var value = new { t = names.Select(name => new { n = name, a = (string?)null }).ToArray() };
            var json = oldEnvelope ? JsonSerializer.Serialize(new { Key = 15, Value = value }) : JsonSerializer.Serialize(new { k = 15, v = value });
            await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
            var etag = (await blob.GetPropertiesAsync()).Value.ETag;
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
            Assert.Equal(35, plan.Clubs.Count); Assert.Equal(35, plan.ResolvedClubCodes.Count);
            Assert.Equal(FantacalcioClubCodes.Names.Values.Order(), plan.Clubs.Select(x => x.Id).Order());
            foreach (var (name, index) in names.Select((name, index) => (name, index)))
            {
                var row = plan.Clubs.Single(x => x.Name == name);
                Assert.Equal(FantacalcioClubCodes.Names[name], row.Id); Assert.Equal(row.Id, row.Abbreviation); Assert.NotNull(row.LegacyId);
                var resolved = plan.ResolvedClubCodes.Single(x => x.JsonPath == (oldEnvelope ? "$.Value" : "$.v") + $".t[{index}]");
                Assert.Equal("realteamwrapper", resolved.Container); Assert.Equal(FantacalcioClubCodes.Version, resolved.CatalogueVersion);
                Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(_dump, resolved.ArchiveReference)));
                Assert.Contains(dataset.Documents, x => x.Sha256 == resolved.DocumentSha256);
            }
            var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(35, await database.Clubs.CountAsync()); Assert.Equal(35, report.ResolvedClubCodes!.Length);
            Assert.False(report.FullMigrationComplete); Assert.Contains(report.PendingFields, x => x.Contains("resolvedClubCodes"));
            Assert.DoesNotContain("Fiorentina", JsonSerializer.Serialize(report));
            var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
            Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
        }
    }

    [CoreFact]
    public async Task Name_only_clubs_from_players_rosters_votes_calendar_and_rank_share_natural_keys_and_keep_finances()
    {
        async Task Put(string containerName, object key, object value)
        {
            var container = _extraContainers.Contains(containerName) ? _storage.Blobs.GetBlobContainerClient(containerName) : await ExtraContainerAsync(containerName);
            await container.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { k = key, v = value })), overwrite: true);
        }
        await Put("realteamwrapper", 15, new { t = new[] { new { n = "Fiorentina", a = (string?)null } } });
        var team = new RealTeam { Name = "Fiorentina", Abbreviation = "" };
        await Put("realplayerswrapper", 15, new RealPlayersWrapper { Players = [new()
            { Name = "Verified club player", Role = Role.Forward, RealTeam = team, IsActive = true }] });
        await Put("team", new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }, new Team
            { Name = "Frozen roster", Owner = Email, MoneyFromRank = 17, OpeningCompetitionPrize = 9, Players = [new()
                { Name = "Verified club player", Role = Role.Forward, RealTeam = team, Price = 37, Revenue = 49, Status = PlayerInTeamStatus.Sold, Position = FantaSoccerRole.Tribune }] });
        await Put("official", new YearDayKey { Year = 15, Day = 2 }, new VotedRealPlayerWrapper { Players = [new()
            { Name = "Verified club player", Role = Role.Forward, RealTeam = team, Vote = new() { Value = 7, HasVote = true, IsFinal = true } }] });
        await Put("realcalendar", 15, new RealCalendar { Year = 15, Days = [new() { Year = 15, SerieADay = 2, Games =
            [new() { Home = team, Away = new() { Name = "Napoli", Abbreviation = "" } }] }] });
        await Put("realrank", 15, new RealRank { Teams = [new() { Name = "Fiorentina", Point = 4.5f }] });
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(new[] { "fio", "nap" }, await database.Clubs.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Equal("fio", (await database.PlayerSeasons.SingleAsync()).ClubId);
        Assert.Equal("fio", (await database.FootballVotes.SingleAsync()).ClubId);
        Assert.Equal("fio", (await database.FootballClubRanks.SingleAsync()).ClubId);
        var calendar = await database.FootballMatches.SingleAsync(); Assert.Equal("fio", calendar.HomeClubId); Assert.Equal("nap", calendar.AwayClubId);
        var roster = await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.Equal(17, roster.MoneyFromRank); Assert.Equal(9, roster.OpeningCompetitionPrize);
        var player = Assert.Single(roster.Players); Assert.Equal("fio", player.ClubId); Assert.Equal(37, player.Price); Assert.Equal(49, player.Revenue);
        Assert.Equal((int)PlayerInTeamStatus.Sold, player.Status); Assert.Equal((int)FantaSoccerRole.Tribune, player.Position);
        Assert.Equal(6, report.ResolvedClubCodes!.Length);
        Assert.Equal(6, report.ResolvedClubCodes.Select(x => (x.DocumentSha256, x.JsonPath)).Distinct().Count());
        Assert.All(report.ResolvedClubCodes, x => Assert.Equal(FantacalcioClubCodes.Version, x.CatalogueVersion));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Equal(2, await database.Clubs.CountAsync());
    }

    [CoreFact]
    public async Task A_conflicting_explicit_club_code_is_not_replaced_by_the_catalogue_and_never_partially_reimports()
    {
        var container = await ExtraContainerAsync("realteamwrapper"); var blob = container.GetBlobClient("source");
        await blob.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Fiorentina\",\"a\":null}]}}"));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        var original = await database.Clubs.AsNoTracking().SingleAsync();
        var checkpoints = await database.CoreImportCheckpoints.AsNoTracking().Select(x => x.Sha256).Order().ToArrayAsync();
        foreach (var code in new[] { "NAP", "FIO!", "fi" })
        {
            await blob.UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { Key = 15, Value = new { t = new[] { new { n = "Fiorentina", a = code } } } })), overwrite: true);
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
            Assert.Equal("invalid_club_code", fault.Message); Assert.Equal("$.Value.t[0]", fault.ClubValidation!.JsonPath);
            if (code == "NAP") Assert.Contains("club_code_name_mismatch", fault.ClubValidation.Reasons);
            Assert.DoesNotContain("Fiorentina", CoreImportDiagnostics.Format(fault));
            var saved = await database.Clubs.AsNoTracking().SingleAsync(); Assert.Equal("fio", saved.Id); Assert.Equal(original.LegacyId, saved.LegacyId);
            Assert.Equal(checkpoints, await database.CoreImportCheckpoints.AsNoTracking().Select(x => x.Sha256).Order().ToArrayAsync());
        }
    }
}
