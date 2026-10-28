using System.Text.Json;
using System.Text.RegularExpressions;
using Fantasoccer.DataMigration;
using Fantasoccer.Domain;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    private const string DiagnosticPlayerName = "private-player-name-sentinel";
    private const string DiagnosticPlayerBlob = "private-player-source@example.test";
    private static Player DiagnosticPlayer(int role = 99, string? name = DiagnosticPlayerName) => new()
    {
        Name = name!, Role = (Role)role, RealTeam = new() { Name = "private-club-sentinel" },
        Position = FantaSoccerRole.Forward
    };

    [CoreFact]
    public async Task Invalid_player_diagnostics_distinguish_invalid_normalized_keys_and_roles_in_both_envelopes_without_logging_values()
    {
        foreach (var oldEnvelope in new[] { false, true })
        foreach (var (invalid, reasons) in new (Player?, string[])[]
        {
            (DiagnosticPlayer(3, "123456789012345"), ["empty_player_key"]),
            (DiagnosticPlayer(), ["role_out_of_range"]),
            (DiagnosticPlayer(99, "!!!"), ["empty_player_key", "role_out_of_range"])
        })
        {
            var wrapper = new RealPlayersWrapper { Players = [DiagnosticPlayer(3, "Valid player"), invalid!] };
            var fault = await RefusePlayerDocumentAsync("realplayerswrapper", 15, wrapper,
                oldEnvelope ? "$.Value.p[1]" : "$.v.p[1]", oldEnvelope: oldEnvelope);
            Assert.Equal(reasons, fault.PlayerValidation!.Reasons);
            Assert.Equal(invalid?.Name is null, fault.PlayerValidation.NameMissing);
            Assert.Equal(invalid?.Name?.Length, fault.PlayerValidation.NameLength);
            Assert.Equal(invalid is null ? (int?)null : (int)invalid.Role, fault.PlayerValidation.Role);
        }

        // Diagnostic enrichment must not tighten the accepted role range or
        // change the mapping/checkpoint behavior for valid documents.
        var container = _storage.Blobs.GetBlobContainerClient("realplayerswrapper");
        await container.GetBlobClient("valid-roles").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            Entity<RealPlayersWrapper, int>.Default(new() { Players = [DiagnosticPlayer(-1, "Undefined player"), DiagnosticPlayer(3, "Forward player")] }, 15))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(2, await database.Players.CountAsync());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(report.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task A_null_roster_entry_is_not_silently_dropped_or_replaced_by_a_fake_player()
    {
        var fault = await RefusePlayerDocumentAsync("team", new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email },
            new Team { Name = "Private team", Owner = Email, Players = [null!] }, "$.v.p[0]");
        Assert.Equal(new[] { "null_player" }, fault.PlayerValidation!.Reasons);
    }

    [CoreFact]
    public async Task Invalid_player_diagnostics_point_to_the_failing_sample_not_an_earlier_valid_catalog_copy()
    {
        foreach (var container in new[] { "live", "official", "notification", "chancedrealplayerwrapper", "statplayerswrapper" })
            await RefusePlayerDocumentAsync(container,
                container == "statplayerswrapper" ? (object)15 : new YearDayKey { Year = 15, Day = 2 },
                new RealPlayersWrapper { Players = [DiagnosticPlayer()] }, "$.v.p[0]");

        var catalog = await ExtraContainerAsync("realplayerswrapper");
        await catalog.GetBlobClient("valid-catalog-copy").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            Entity<RealPlayersWrapper, int>.Default(new() { Players = [DiagnosticPlayer(3)] }, 15))));
        foreach (var container in new[] { "team", "dailyteams", "openingteams", "openingdailyteams" })
        {
            object key = container switch
            {
                "team" => new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email },
                "dailyteams" => new TeamDayKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email, Day = 2 },
                "openingteams" => new OpeningTeamKey { Group = "first", BasketId = "same-local-basket", Year = 16, Email = Email, League = "league" },
                _ => new OpeningTeamDayKey { Group = "first", BasketId = "same-local-basket", Year = 16, Email = Email, League = "league", Day = 2 }
            };
            await RefusePlayerDocumentAsync(container, key, new Team { Name = "Private team", Owner = Email, Players = [DiagnosticPlayer()] }, "$.v.p[0]");
        }
    }

    [CoreFact]
    public async Task Nested_player_sources_report_exact_hall_auction_market_played_game_and_push_paths()
    {
        var hallKey = new GeneralLeagueKey { Group = "first", League = "league" };
        await RefusePlayerDocumentAsync("halloffame", hallKey, new HallOfFame { RecordPlayer = new() { Player = DiagnosticPlayer(), Points = 1 } }, "$.v.rp.p");
        await RefusePlayerDocumentAsync("halloffame", hallKey, new HallOfFame { PlayerWithMostPointsInYear = new() { Player = DiagnosticPlayer(), Points = 1, Year = 14 } }, "$.v.ppmy.p");
        await RefusePlayerDocumentAsync("halloffame", hallKey, new HallOfFame { WinningPlayers = [
            new() { Player = DiagnosticPlayer(3, "Valid record") }, new() { Player = DiagnosticPlayer() }] }, "$.v.wp[1].p");

        foreach (var web in new[] { false, true })
            await RefusePlayerDocumentAsync("pushnotificationsent", new YearDayKey { Year = 15, Day = 2 },
                new PushNotificationSent { MatchEventVotes = new() { Players = [new() { Name = DiagnosticPlayerName, Role = (Role)99 }] } },
                web ? "$.v.matchEventVotes.p[0]" : "$.v.MatchEventVotes.p[0]", web: web);
        await RefusePlayerDocumentAsync("pushnotificationsent", new YearDayKey { Year = 15, Day = 2 },
            new { MatchEventVotes = new { P = new[] { DiagnosticPlayer() } } }, "$.v.MatchEventVotes.P[0]");

        var leagueKey = new LeagueKey { Group = "first", League = "league", Year = 15 };
        await RefusePlayerDocumentAsync("auction", "private-auction", new AuctionWrapper { Id = "private-auction", LeagueKey = leagueKey,
            Creator = Email, Created = DateTime.UnixEpoch, LastShow = [], Actual = new() { Player = new() { Name = DiagnosticPlayerName, Role = (Role)99 }, Started = DateTime.UnixEpoch } }, "$.v.a.pl");

        _first.Baskets[0].Years[0].Teams[0].AdditionalOwners = []; // one team per user and league
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Seller team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        foreach (var fromBuyer in new[] { true, false })
            await RefusePlayerDocumentAsync("marketwrapper", leagueKey, new MarketWrapper { Markets = [new()
            {
                Id = "private-trade", Buyer = Email, Seller = OtherEmail, CreationTime = DateTime.UnixEpoch,
                BuyerPlayers = fromBuyer ? [DiagnosticPlayer()] : [], SellerPlayers = fromBuyer ? [] : [DiagnosticPlayer()]
            }] }, fromBuyer ? "$.v.m[0].bp[0]" : "$.v.m[0].sp[0]");
        await RefusePlayerDocumentAsync("randomizedplayers", leagueKey, new RealPlayersWrapper { Players = [DiagnosticPlayer()] }, "$.v.p[0]");

        var calendars = await ExtraContainerAsync("calendar");
        await calendars.GetBlobClient("calendar").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Calendar, LeagueKey>.Default(
            new() { Year = 15, Rounds = new() { ["private-stage"] = [new() { Number = 1, SerieADay = 2,
                Games = [new() { Id = "private-match", Home = "Home", HomeOwner = Email, Away = "Away", AwayOwner = OtherEmail }] }] } }, leagueKey))));
        foreach (var home in new[] { true, false })
            await RefusePlayerDocumentAsync("playedgame", new GameKey { Group = "first", League = "league", Year = 15, Day = 2, GameId = "private-match" },
                new PlayedGame { Id = "private-match", Home = "Home", Away = "Away",
                    HomePlayers = home ? [DiagnosticPlayer()] : [], AwayPlayers = home ? [] : [DiagnosticPlayer()] },
                home ? "$.v['1'][0]" : "$.v['2'][0]");
    }

    private async Task<CoreImportException> RefusePlayerDocumentAsync(string containerName, object key, object value, string expectedPath,
        bool oldEnvelope = false, bool web = false)
    {
        var container = _extraContainers.Contains(containerName) ? _storage.Blobs.GetBlobContainerClient(containerName) : await ExtraContainerAsync(containerName);
        var options = web ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : new JsonSerializerOptions();
        var json = oldEnvelope ? JsonSerializer.Serialize(new { Key = key, Value = value }, options) : JsonSerializer.Serialize(new { k = key, v = value }, options);
        var blob = container.GetBlobClient(DiagnosticPlayerBlob);
        await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
        var etag = (await blob.GetPropertiesAsync()).Value.ETag;
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var hash = manifest.Containers.Single(x => x.Name == containerName).Blobs.Single(x => x.Name == DiagnosticPlayerBlob).Sha256;
        await using var database = fixture.CreateContext();
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("invalid_player", fault.Message); Assert.Equal(hash, fault.DocumentSha256);
        Assert.Equal("player", fault.Field); Assert.NotNull(fault.Reader);
        var details = Assert.IsType<PlayerValidationDetails>(fault.PlayerValidation);
        Assert.Equal(containerName, details.Container); Assert.Equal(expectedPath, details.JsonPath);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_dump, details.ArchiveReference));
        Assert.Equal(hash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        using var document = JsonDocument.Parse(bytes);
        var fragment = document.RootElement;
        var consumed = "$";
        foreach (Match match in Regex.Matches(details.JsonPath[1..], @"\.([A-Za-z][A-Za-z0-9]*)|\['([12])'\]|\[(\d+)\]"))
        {
            consumed += match.Value;
            fragment = match.Groups[3].Success ? fragment[int.Parse(match.Groups[3].Value)]
                : fragment.GetProperty(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        Assert.Equal(details.JsonPath, consumed);
        if (details.Reasons.Contains("null_player")) Assert.Equal(JsonValueKind.Null, fragment.ValueKind);
        else Assert.Equal(details.Role, fragment.GetProperty("r").GetInt32());
        var formatted = CoreImportDiagnostics.Format(fault);
        Assert.Contains("Player details:", formatted); Assert.Contains(hash, formatted); Assert.Contains("No partial transaction was committed.", formatted);
        foreach (var secret in new[] { DiagnosticPlayerName, DiagnosticPlayerBlob, "private-club-sentinel", "Private team", Email, OtherEmail, "private-auction", "private-trade", "private-match", "private-stage", "/root/" })
            Assert.DoesNotContain(secret, formatted);
        Assert.Empty(await database.Users.ToArrayAsync()); Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync());
        Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
        Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
        await container.DeleteBlobAsync(DiagnosticPlayerBlob); // Only this test's synthetic source, after immutability assertions.
        return fault;
    }
}
