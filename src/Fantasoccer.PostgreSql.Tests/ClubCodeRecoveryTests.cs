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
        var count = names.Length;
        await using var database = fixture.CreateContext();
        foreach (var oldEnvelope in new[] { true, false })
        {
            var value = new { t = names.Select(name => new { n = name, a = (string?)null }).ToArray() };
            var json = oldEnvelope ? JsonSerializer.Serialize(new { Key = 15, Value = value }) : JsonSerializer.Serialize(new { k = 15, v = value });
            await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
            var etag = (await blob.GetPropertiesAsync()).Value.ETag;
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
            Assert.Equal(count, plan.Clubs.Count); Assert.Equal(count, plan.ResolvedClubCodes.Count);
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
            Assert.Equal(count, await database.Clubs.CountAsync()); Assert.Equal(count, report.ResolvedClubCodes!.Length);
            Assert.False(report.FullMigrationComplete); Assert.Contains(report.PendingFields, x => x.Contains("resolvedClubCodes"));
            Assert.DoesNotContain("Fiorentina", JsonSerializer.Serialize(report));
            var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
            Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
        }
    }

    [CoreFact]
    public async Task The_full_2012_13_name_only_club_catalogue_preserves_labels_positions_and_source_bytes_and_is_idempotent()
    {
        // Public Serie A club labels only; no real manifest, account, Blob name
        // or document checksum from the operator's dump is part of this fixture.
        string[] names = ["Fiorentina", "Udinese", "Juventus", "Parma", "Milan", "Sampdoria", "Atalanta", "Lazio", "Chievo", "Bologna",
            "Genoa", "Cagliari", "Palermo", "Napoli", "Pescara", "Inter", "Roma", "Catania", "Siena", "Torino"];
        string[] codes = ["fio", "udi", "juv", "par", "mil", "sam", "ata", "laz", "chi", "bol",
            "gen", "cag", "pal", "nap", "pes", "int", "rom", "cat", "sie", "tor"];
        var container = await ExtraContainerAsync("realteamwrapper"); var blob = container.GetBlobClient("season-one-clubs");
        var json = JsonSerializer.Serialize(new { Key = 1, Value = new { t = names.Select(name => new { n = name, a = (string?)null }).ToArray() } });
        await blob.UploadAsync(BinaryData.FromString(json)); var etag = (await blob.GetPropertiesAsync()).Value.ETag;
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (_, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        Assert.Equal(names, plan.Clubs.Select(x => x.Name)); Assert.Equal(codes, plan.Clubs.Select(x => x.Id));
        Assert.Equal(20, plan.ResolvedClubCodes.Count);
        foreach (var index in Enumerable.Range(0, names.Length))
        {
            var recovery = plan.ResolvedClubCodes.Single(x => x.JsonPath == $"$.Value.t[{index}]");
            Assert.Equal(FantacalcioClubCodes.Version, recovery.CatalogueVersion);
            Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(_dump, recovery.ArchiveReference)));
        }
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(20, await database.Clubs.CountAsync()); Assert.Equal(20, report.ResolvedClubCodes!.Length);
        Assert.Equal(2012, (await database.Seasons.SingleAsync(x => x.SeasonIndex == 1)).StartYear);
        Assert.Equal("cat", (await database.Clubs.SingleAsync(x => x.Name == "Catania")).Id);
        Assert.Equal("sie", (await database.Clubs.SingleAsync(x => x.Name == "Siena")).Id);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
        Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
    }

    [CoreFact]
    public async Task All_cross_year_catalogue_name_code_pairs_resolve_together_in_either_order_without_duplicate_clubs_or_source_changes()
    {
        // Public football labels/codes only, covering the complete cross-year
        // catalogue union; no manifest, original Blob name or account data.
        (string Name, string? Abbreviation, string ExpectedCode)[] samples =
        [
            ("Atalanta", null, "ata"), ("Atalanta", "ata", "ata"), ("Benevento", null, "ben"),
            ("Bologna", null, "bol"), ("Bologna", "bol", "bol"), ("Brescia", null, "bre"),
            ("Cagliari", null, "cag"), ("Cagliari", "cag", "cag"), ("Carpi", null, "car"),
            ("Catania", null, "cat"), ("Cesena", null, "ces"), ("Chievo", null, "chi"),
            ("Como", null, "com"), ("Como", "com", "com"), ("Cremonese", null, "cre"),
            ("Cremonese", "cre", "cre"), ("Crotone", null, "cro"), ("Empoli", null, "emp"),
            ("Fiorentina", null, "fio"), ("Fiorentina", "fio", "fio"), ("Frosinone", null, "fro"),
            ("Frosinone", "fro", "fro"), ("Genoa", null, "gen"), ("Genoa", "gen", "gen"),
            ("Inter", null, "int"), ("Inter", "int", "int"), ("Juve", null, "juv"),
            ("Juventus", null, "juv"), ("Juventus", "juv", "juv"), ("Lazio", null, "laz"),
            ("Lazio", "laz", "laz"), ("Lecce", null, "lec"), ("Lecce", "lec", "lec"),
            ("Livorno", null, "liv"), ("Milan", null, "mil"), ("Milan", "mil", "mil"),
            ("Monza", null, "mon"), ("Monza", "mon", "mon"), ("Napoli", null, "nap"),
            ("Napoli", "nap", "nap"), ("Palermo", null, "pal"), ("Parma", null, "par"),
            ("Parma", "par", "par"), ("Pescara", null, "pes"), ("Pisa", "pis", "pis"),
            ("Roma", null, "rom"), ("Roma", "rom", "rom"), ("Salernitana", null, "sal"),
            ("Sampdoria", null, "sam"), ("Sassuolo", null, "sas"), ("Sassuolo", "sas", "sas"),
            ("Siena", null, "sie"), ("Spal", null, "spa"), ("Spezia", null, "spe"),
            ("Torino", null, "tor"), ("Torino", "tor", "tor"), ("Udinese", null, "udi"),
            ("Udinese", "udi", "udi"), ("Venezia", null, "ven"), ("Venezia", "ven", "ven"),
            ("Verona", null, "ver"), ("Verona", "ver", "ver")
        ];
        Assert.Equal(62, samples.Length); Assert.Equal(40, samples.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(39, samples.Select(x => x.ExpectedCode).Distinct().Count());
        foreach (var sample in samples) Assert.Equal(sample.ExpectedCode, KeyUtils.GetClubKey(sample.Abbreviation, sample.Name));
        var container = await ExtraContainerAsync("realteamwrapper");
        await using var database = fixture.CreateContext();
        foreach (var reversed in new[] { false, true })
        {
            var ordered = reversed ? samples.Reverse().ToArray() : samples;
            var sources = new List<(Azure.Storage.Blobs.BlobClient Blob, string Json, Azure.ETag ETag)>();
            foreach (var explicitCode in new[] { false, true })
            {
                var rows = ordered.Where(x => (x.Abbreviation is not null) == explicitCode).Select(x => new { n = x.Name, a = x.Abbreviation }).ToArray();
                var year = explicitCode ? 15 : 1;
                var json = JsonSerializer.Serialize(new { Key = year, Value = new { t = rows } });
                var blob = container.GetBlobClient(explicitCode ? "b-explicit-codes" : "a-name-only");
                await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
                sources.Add((blob, json, (await blob.GetPropertiesAsync()).Value.ETag));
            }
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var (_, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
            Assert.Equal(39, plan.Clubs.Count); Assert.Single(plan.Clubs.Where(x => x.Id == "juv"));
            Assert.Equal("Juventus", plan.Clubs.Single(x => x.Id == "juv").Name);
            Assert.Equal("SPAL", plan.Clubs.Single(x => x.Id == "spa").Name);
            Assert.Equal(61, plan.ClubSeasons.Count);
            Assert.Equal(2, plan.ClubSeasons.Count(x => x.ClubId == "juv"));
            Assert.Equal(samples.Select(x => x.ExpectedCode).Distinct().Order(), plan.Clubs.Select(x => x.Id).Order());
            Assert.Equal(39, plan.ResolvedClubCodes.Count);
            Assert.All(plan.ResolvedClubCodes, x => Assert.Equal(FantacalcioClubCodes.Version, x.CatalogueVersion));
            var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(39, await database.Clubs.CountAsync()); Assert.Equal(1, await database.Clubs.CountAsync(x => x.Id == "juv"));
            Assert.Equal(61, await database.ClubSeasons.CountAsync()); Assert.Equal(new[] { 1, 15 }, report.ClubCatalogueYears);
            Assert.Equal(39, report.ResolvedClubCodes!.Length);
            var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            foreach (var (blob, json, etag) in sources)
            {
                Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
                Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
            }
        }
    }

    [CoreFact]
    public async Task Exact_Juve_alias_links_rosters_votes_and_player_years_to_one_club_but_never_overrides_an_explicit_conflict()
    {
        var clubs = await ExtraContainerAsync("realteamwrapper"); var source = clubs.GetBlobClient("alias-catalogue");
        await source.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Juve\",\"a\":null},{\"n\":\"Juventus\",\"a\":null}]}}"));
        var catalogues = await ExtraContainerAsync("realplayerswrapper");
        await catalogues.GetBlobClient("14").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealPlayersWrapper, int>.Default(new()
            { Players = [new() { Name = "Alias fixture player", Role = Role.Forward, RealTeam = new() { Name = "Juventus", Abbreviation = "JUV" } }] }, 14))));
        var votes = await ExtraContainerAsync("official");
        await votes.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new()
            { Players = [new() { Name = "Alias fixture player", Role = Role.Forward, RealTeam = new() { Name = "Juventus", Abbreviation = "" },
                Vote = new() { Value = 7, HasVote = true, IsFinal = true } }] }, new() { Year = 15, Day = 2 }))));
        var rosters = await ExtraContainerAsync("team");
        await rosters.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(new()
            { Name = "Alias fixture roster", Owner = Email, Players = [new() { Name = "Alias fixture player", Role = Role.Forward,
                RealTeam = new() { Name = "Juve", Abbreviation = "" }, Price = 41, Revenue = 58, Status = PlayerInTeamStatus.Sold, Position = FantaSoccerRole.Tribune }] },
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal("juv", (await database.Clubs.SingleAsync()).Id);
        Assert.Equal("Juventus", (await database.Clubs.SingleAsync()).Name);
        Assert.Equal(new[] { 14, 15 }, await database.PlayerSeasons.OrderBy(x => x.SeasonIndex).Select(x => x.SeasonIndex).ToArrayAsync());
        Assert.All(await database.PlayerSeasons.ToArrayAsync(), x => Assert.Equal("juv", x.ClubId));
        Assert.Equal("juv", (await database.FootballVotes.SingleAsync()).ClubId);
        var player = (await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync()).Players.Single();
        Assert.Equal("juv", player.ClubId); Assert.Equal(41, player.Price); Assert.Equal(58, player.Revenue);
        Assert.Equal((int)PlayerInTeamStatus.Sold, player.Status); Assert.Equal((int)FantaSoccerRole.Tribune, player.Position);
        Assert.Contains(report.ResolvedClubCodes!, x => x.Container == "team" && x.JsonPath == "$.v.p[0].t");
        Assert.DoesNotContain("Juve", JsonSerializer.Serialize(report));
        var checkpoints = await database.CoreImportCheckpoints.AsNoTracking().Select(x => x.Sha256).Order().ToArrayAsync();
        await source.UploadAsync(BinaryData.FromString("{\"Key\":15,\"Value\":{\"t\":[{\"n\":\"Juve\",\"a\":\"NAP\"}]}}"), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Contains("club_code_name_mismatch", fault.ClubValidation!.Reasons);
        Assert.DoesNotContain("Juve", CoreImportDiagnostics.Format(fault));
        Assert.Equal(checkpoints, await database.CoreImportCheckpoints.AsNoTracking().Select(x => x.Sha256).Order().ToArrayAsync());
        Assert.Equal("juv", (await database.Clubs.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(41, (await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync()).Players.Single().Price);
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
        foreach (var code in new[] { "NAP", "mil" })
        {
            await blob.UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { Key = 15, Value = new { t = new[] { new { n = "Fiorentina", a = code } } } })), overwrite: true);
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
            Assert.Equal("invalid_club_code", fault.Message); Assert.Equal("$.Value.t[0]", fault.ClubValidation!.JsonPath);
            Assert.Contains("club_code_name_mismatch", fault.ClubValidation.Reasons);
            Assert.DoesNotContain("Fiorentina", CoreImportDiagnostics.Format(fault));
            var saved = await database.Clubs.AsNoTracking().SingleAsync(); Assert.Equal("fio", saved.Id); Assert.Equal(original.LegacyId, saved.LegacyId);
            Assert.Equal(checkpoints, await database.CoreImportCheckpoints.AsNoTracking().Select(x => x.Sha256).Order().ToArrayAsync());
        }
    }

    [CoreFact]
    public async Task Malformed_provider_codes_next_to_a_verified_name_resolve_to_the_verified_club_and_are_reported()
    {
        // "MONZ" is what the provider sends for Monza; "FIO!" and "fi" are not codes at all.
        var container = await ExtraContainerAsync("realteamwrapper");
        await container.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { Key = 15, Value = new { t = new[]
            { new { n = "Fiorentina", a = "FIO!" }, new { n = "Monza", a = "MONZ" }, new { n = "AC Milan", a = "fi" }, new { n = "Napoli", a = "nap" } } } })));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(new[] { ("fio", "Fiorentina"), ("mil", "Milan"), ("mon", "Monza"), ("nap", "Napoli") },
            (await database.Clubs.OrderBy(x => x.Id).ToArrayAsync()).Select(x => (x.Id, x.Name)));
        Assert.Equal(new[] { "$.Value.t[0]", "$.Value.t[1]", "$.Value.t[2]" },
            report.ResolvedClubCodes!.Where(x => x.Container == "realteamwrapper").Select(x => x.JsonPath).Order());
        Assert.DoesNotContain("MONZ", System.Text.Json.JsonSerializer.Serialize(report));
    }
}
