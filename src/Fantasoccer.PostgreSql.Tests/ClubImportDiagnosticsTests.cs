using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string DiagnosticClubName = "private-club-label-sentinel";
    private const string DiagnosticClubCode = "private-rejected-code";
    private const string DiagnosticClubBlob = "private-club-source@example.test";
    private static RealTeam ValidDiagnosticClub() => new() { Name = "Verified fixture club", Abbreviation = "ver" };
    private static RealTeam InvalidDiagnosticClub() => new() { Name = DiagnosticClubName, Abbreviation = DiagnosticClubCode };
    private static Player ClubDiagnosticPlayer(bool invalid = true) => new()
    {
        Name = DiagnosticPlayerName, Role = Role.Forward, RealTeam = invalid ? InvalidDiagnosticClub() : ValidDiagnosticClub(),
        Position = FantaSoccerRole.Forward, Price = 37, Revenue = 49
    };

    [CoreFact]
    public async Task Club_diagnostics_distinguish_missing_codes_lengths_characters_and_null_clubs_in_both_envelopes()
    {
        foreach (var oldEnvelope in new[] { false, true })
        foreach (var (rawClub, code, reason) in new[]
        {
            ("{\"n\":\"private-club-label-sentinel\"}", "invalid_club_code", "missing_club_code"),
            ("{\"n\":\"private-club-label-sentinel\",\"a\":\"private-rejected-code\"}", "invalid_club_code", "club_code_length"),
            ("{\"n\":\"private-club-label-sentinel\",\"a\":\"N4P\"}", "invalid_club_code", "club_code_not_ascii_letters"),
            ("{\"n\":\"private-club-label-sentinel\",\"a\":\"ÅBC\"}", "invalid_club_code", "club_code_not_ascii_letters"),
            ("{\"n\":\"private-club-label-sentinel\",\"a\":null}", "invalid_club_code", "missing_club_code"),
            ("{\"n\":\"private-club-label-sentinel\",\"a\":\" \"}", "invalid_club_code", "missing_club_code"),
            ("null", "missing_real_club", "null_club"),
            ("{\"n\":null,\"a\":\"VER\"}", "missing_real_club", "empty_club_name"),
            ("{\"n\":\"\",\"a\":\"VER\"}", "missing_real_club", "empty_club_name")
        })
        {
            var value = new JsonObject { ["t"] = new JsonArray(JsonSerializer.SerializeToNode(ValidDiagnosticClub()), JsonNode.Parse(rawClub)) };
            var fault = await RefuseClubDocumentAsync("realteamwrapper", 15, value,
                oldEnvelope ? "$.Value.t[1]" : "$.v.t[1]", code, oldEnvelope: oldEnvelope);
            Assert.Contains(reason, fault.ClubValidation!.Reasons);
            var source = JsonNode.Parse(rawClub);
            Assert.Equal(source?["n"]?.GetValue<string>() is null, fault.ClubValidation.NameMissing);
            Assert.Equal(source?["n"]?.GetValue<string>()?.Length, fault.ClubValidation.NameLength);
            Assert.Equal(string.IsNullOrWhiteSpace(source?["a"]?.GetValue<string>()), fault.ClubValidation.AbbreviationMissing);
            Assert.Equal(source?["a"]?.GetValue<string>()?.Length, fault.ClubValidation.AbbreviationLength);
        }
    }

    [CoreFact]
    public async Task Club_diagnostics_point_to_every_failing_sample_even_after_a_valid_global_and_annual_player_are_known()
    {
        var catalog = await ExtraContainerAsync("realplayerswrapper");
        await catalog.GetBlobClient("verified-catalog").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
            Entity<RealPlayersWrapper, int>.Default(new() { Players = [ClubDiagnosticPlayer(false)] }, 15))));
        foreach (var container in new[] { "live", "official", "notification", "chancedrealplayerwrapper", "statplayerswrapper" })
        {
            object value = container switch
            {
                "statplayerswrapper" => new StatPlayersWrapper { Players = [new() { Name = DiagnosticPlayerName, Role = Role.Forward, RealTeam = InvalidDiagnosticClub() }] },
                "chancedrealplayerwrapper" => new ChancedRealPlayerWrapper { Players = [new() { Name = DiagnosticPlayerName, Role = Role.Forward, RealTeam = InvalidDiagnosticClub(), Chance = new() }] },
                _ => new VotedRealPlayerWrapper { Players = [new() { Name = DiagnosticPlayerName, Role = Role.Forward, RealTeam = InvalidDiagnosticClub(), Vote = new() { Value = 7, HasVote = true } }] }
            };
            await RefuseClubDocumentAsync(container, container == "statplayerswrapper" ? (object)15 : new YearDayKey { Year = 15, Day = 2 },
                value, "$.v.p[0].t");
        }
        foreach (var container in new[] { "team", "dailyteams", "openingteams", "openingdailyteams" })
        {
            object key = container switch
            {
                "team" => new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email },
                "dailyteams" => new TeamDayKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email, Day = 2 },
                "openingteams" => new OpeningTeamKey { Group = "first", BasketId = "same-local-basket", Year = 16, Email = Email, League = "league" },
                _ => new OpeningTeamDayKey { Group = "first", BasketId = "same-local-basket", Year = 16, Email = Email, League = "league", Day = 2 }
            };
            await RefuseClubDocumentAsync(container, key, new Team { Name = "Private team", Owner = Email, Players = [ClubDiagnosticPlayer()] }, "$.v.p[0].t");
        }
    }

    [CoreFact]
    public async Task Club_diagnostics_cover_calendar_sides_and_unknown_name_only_standings_without_inventing_codes()
    {
        foreach (var home in new[] { true, false })
            await RefuseClubDocumentAsync("realcalendar", 15, new RealCalendar { Year = 15, Days =
                [new() { Year = 15, SerieADay = 1, Games = [] }, new() { Year = 15, SerieADay = 2, Games =
                    [new() { Home = home ? InvalidDiagnosticClub() : ValidDiagnosticClub(), Away = home ? ValidDiagnosticClub() : InvalidDiagnosticClub() }] }] },
                home ? "$.v.d[1].g[0].h" : "$.v.d[1].g[0].a");
        await RefuseClubDocumentAsync("realrank", 15, new RealRank { Teams = [new() { Name = DiagnosticClubName, Point = 4.5f }] }, "$.v.r[0]");
    }

    [CoreFact]
    public async Task Nested_club_diagnostics_preserve_hall_market_auction_played_game_and_case_insensitive_push_coordinates()
    {
        var hallKey = new GeneralLeagueKey { Group = "first", League = "league" };
        await RefuseClubDocumentAsync("halloffame", hallKey, new HallOfFame { RecordPlayer = new() { Player = ClubDiagnosticPlayer(), Points = 1 } }, "$.v.rp.p.t");
        await RefuseClubDocumentAsync("halloffame", hallKey, new HallOfFame { PlayerWithMostPointsInYear = new() { Player = ClubDiagnosticPlayer(), Points = 1, Year = 14 } }, "$.v.ppmy.p.t");
        await RefuseClubDocumentAsync("halloffame", hallKey, new HallOfFame { WinningPlayers =
            [new() { Player = ClubDiagnosticPlayer(false) }, new() { Player = ClubDiagnosticPlayer() }] }, "$.v.wp[1].p.t");
        foreach (var uppercase in new[] { false, true })
        {
            var sample = JsonSerializer.SerializeToNode(new VotedRealPlayer { Name = DiagnosticPlayerName, Role = Role.Forward,
                RealTeam = InvalidDiagnosticClub(), Vote = new() { Value = 7, HasVote = true } })!.AsObject();
            if (uppercase)
            {
                var team = sample["t"]; sample.Remove("t"); sample["T"] = team;
            }
            var value = new JsonObject { [uppercase ? "MatchEventVotes" : "matchEventVotes"] = new JsonObject
                { [uppercase ? "P" : "p"] = new JsonArray(sample) } };
            await RefuseClubDocumentAsync("pushnotificationsent", new YearDayKey { Year = 15, Day = 2 }, value,
                uppercase ? "$.v.MatchEventVotes.P[0].T" : "$.v.matchEventVotes.p[0].t");
        }
        var leagueKey = new LeagueKey { Group = "first", League = "league", Year = 15 };
        await RefuseClubDocumentAsync("auction", "private-auction", new AuctionWrapper { Id = "private-auction", LeagueKey = leagueKey,
            Creator = Email, Created = DateTime.UnixEpoch, LastShow = [], Actual = new() { Player = new()
                { Name = DiagnosticPlayerName, Role = Role.Forward, RealTeam = InvalidDiagnosticClub() }, Started = DateTime.UnixEpoch } }, "$.v.a.pl.t");
        await RefuseClubDocumentAsync("randomizedplayers", leagueKey, new RealPlayersWrapper { Players = [ClubDiagnosticPlayer()] }, "$.v.p[0].t");
        // Excluded containers must not be decoded merely to look for club codes.
        var excluded = _storage.Blobs.GetBlobContainerClient("auction");
        await excluded.GetBlobClient(DiagnosticClubBlob).UploadAsync(BinaryData.FromString("not-json-private-auction"));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (_, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump, options: new(ImportLegacyAuctions: false));
        Assert.Empty(plan.Auctions);
        await excluded.DeleteBlobAsync(DiagnosticClubBlob);
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Seller team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        foreach (var fromBuyer in new[] { true, false })
            await RefuseClubDocumentAsync("marketwrapper", leagueKey, new MarketWrapper { Markets = [new()
            {
                Id = "private-trade", Buyer = Email, Seller = OtherEmail, CreationTime = DateTime.UnixEpoch,
                BuyerPlayers = fromBuyer ? [ClubDiagnosticPlayer()] : [], SellerPlayers = fromBuyer ? [] : [ClubDiagnosticPlayer()]
            }] }, fromBuyer ? "$.v.m[0].bp[0].t" : "$.v.m[0].sp[0].t");
        var calendars = await ExtraContainerAsync("calendar");
        await calendars.GetBlobClient("calendar").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Calendar, LeagueKey>.Default(
            new() { Year = 15, Rounds = new() { ["private-stage"] = [new() { Number = 1, SerieADay = 2,
                Games = [new() { Id = "private-match", Home = "Home", HomeOwner = Email, Away = "Away", AwayOwner = OtherEmail }] }] } }, leagueKey))));
        foreach (var home in new[] { true, false })
            await RefuseClubDocumentAsync("playedgame", new GameKey { Group = "first", League = "league", Year = 15, Day = 2, GameId = "private-match" },
                new PlayedGame { Id = "private-match", Home = "Home", Away = "Away",
                    HomePlayers = home ? [ClubDiagnosticPlayer()] : [], AwayPlayers = home ? [] : [ClubDiagnosticPlayer()] },
                home ? "$.v['1'][0].t" : "$.v['2'][0].t");
    }

    private async Task<CoreImportException> RefuseClubDocumentAsync(string containerName, object key, object value, string expectedPath,
        string expectedCode = "invalid_club_code", bool oldEnvelope = false)
    {
        var container = _extraContainers.Contains(containerName) ? _storage.Blobs.GetBlobContainerClient(containerName) : await ExtraContainerAsync(containerName);
        var json = oldEnvelope ? JsonSerializer.Serialize(new { Key = key, Value = value }) : JsonSerializer.Serialize(new { k = key, v = value });
        var blob = container.GetBlobClient(DiagnosticClubBlob);
        await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
        var etag = (await blob.GetPropertiesAsync()).Value.ETag;
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var hash = manifest.Containers.Single(x => x.Name == containerName).Blobs.Single(x => x.Name == DiagnosticClubBlob).Sha256;
        await using var database = fixture.CreateContext();
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal(expectedCode, fault.Message); Assert.Equal(hash, fault.DocumentSha256); Assert.Equal("club", fault.Field);
        Assert.Matches(@"\.cs:\d+$", fault.Reader!); Assert.Null(fault.PlayerValidation);
        var details = Assert.IsType<ClubValidationDetails>(fault.ClubValidation);
        Assert.Equal(containerName, details.Container); Assert.Equal(expectedPath, details.JsonPath); Assert.NotEmpty(details.Reasons);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_dump, details.ArchiveReference));
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
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
        Assert.Equal(details.Reasons.Contains("null_club") ? JsonValueKind.Null : JsonValueKind.Object, fragment.ValueKind);
        var formatted = CoreImportDiagnostics.Format(fault);
        Assert.Contains("Club details:", formatted); Assert.Contains(hash, formatted); Assert.Contains("No partial transaction was committed.", formatted);
        foreach (var secret in new[] { DiagnosticClubName, DiagnosticClubCode, DiagnosticClubBlob, DiagnosticPlayerName,
            "Private team", Email, OtherEmail, "private-auction", "private-trade", "private-match", "private-stage", "/root/" })
            Assert.DoesNotContain(secret, formatted);
        Assert.Empty(await database.Users.ToArrayAsync()); Assert.Empty(await database.Clubs.ToArrayAsync());
        Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync());
        Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
        Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
        await container.DeleteBlobAsync(DiagnosticClubBlob); // Only this test's synthetic source after immutability assertions.
        return fault;
    }
}
