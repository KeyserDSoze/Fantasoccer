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
    private async Task PutLegacyDocumentAsync<T>(string containerName, int key, T value)
    {
        var container = _extraContainers.Contains(containerName) ? _storage.Blobs.GetBlobContainerClient(containerName) : await ExtraContainerAsync(containerName);
        await container.GetBlobClient(key.ToString()).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<T, int>.Default(value, key))), overwrite: true);
    }

    private static RealGame CalendarGame(string home, string? homeCode, string away, string? awayCode, DateTime date, int? homeGoals = null, int? awayGoals = null, bool delayed = false)
    {
        RealTeam Team(string name, string? code) => code is null ? new() { Name = name } : new() { Name = name, Abbreviation = code };
        return new() { Home = Team(home, homeCode), Away = Team(away, awayCode), Date = date, HomeGoal = homeGoals, AwayGoal = awayGoals, Delayed = delayed };
    }

    [CoreFact]
    public async Task Next_season_calendar_saved_under_the_previous_year_is_excluded_and_verona_code_variants_share_one_club()
    {
        // Season 12 (2023/24) was overwritten in summer 2024 with 2024/25 games
        // and provider labels without codes; it must not become 2023/24 data.
        await PutLegacyDocumentAsync("realcalendar", 12, new RealCalendar { Year = 12, Days = [new() { Year = 12, SerieADay = 1, Games =
            [CalendarGame("Como 1907", null, "Juventus", null, new(2024, 8, 18, 18, 30, 0)),
             CalendarGame("Venezia FC", null, "Lazio", null, new(2024, 8, 19, 20, 45, 0), delayed: true)] }] });
        await PutLegacyDocumentAsync("realcalendar", 13, new RealCalendar { Year = 13, Days = [new() { Year = 13, SerieADay = 1, Games =
            [CalendarGame("Hellas verona", "hel", "Como 1907", "com", new(2024, 8, 18, 18, 30, 0), 1, 0),
             CalendarGame("Juventus", "juv", "Hellas verona", "ver", new(2024, 8, 19, 20, 45, 0), 3, 0),
             CalendarGame("Venezia FC", null, "Como 1907", null, new(2024, 8, 20, 20, 45, 0), 0, 2)] }] });
        await PutLegacyDocumentAsync("realrank", 13, new RealRank { Teams = [new() { Name = "HELLAS VERONA", Point = 3 }, new() { Name = "COMO", Point = 0 }] });
        // Current season: a played game keeps its score, a future placeholder does not.
        var year = FantacalcioTime.ActualYear; var now = FantacalcioTime.ItalyNow;
        await PutLegacyDocumentAsync("realcalendar", year, new RealCalendar { Year = year, Days = [new() { Year = year, SerieADay = 1, Games =
            [CalendarGame("Milan", "mil", "Inter", "int", now.AddHours(-3), 2, 1), CalendarGame("Roma", "rom", "Monza", "monz", now.AddDays(2), 0, 0)] }] });
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        string Hash(int key) => manifest.Containers.Single(x => x.Name == "realcalendar").Blobs.Single(x => x.Name == key.ToString()).Sha256;
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);

        var excluded = Assert.Single(report.ExcludedRealCalendars!);
        Assert.Equal((Hash(12), 12, 1, 2), (excluded.DocumentSha256, excluded.SeasonIndex, excluded.Days, excluded.Games));
        Assert.Equal(new[] { 13 }, excluded.ContentSeasonIndexes);
        Assert.Equal(new DateTime(2024, 8, 18, 18, 30, 0), excluded.FirstKickoffLocal); Assert.Equal(new DateTime(2024, 8, 19, 20, 45, 0), excluded.LastKickoffLocal);
        Assert.Contains(report.PreservedArtifacts!, x => x.Contains(Hash(12), StringComparison.Ordinal));
        Assert.False(await database.FootballMatchdays.AnyAsync(x => x.SeasonIndex == 12));
        Assert.False(await database.ClubSeasons.AnyAsync(x => x.SeasonIndex == 12));
        Assert.False(await database.FootballDatasets.AnyAsync(x => x.Kind == "realcalendar" && x.SeasonIndex == 12));

        // "hel" and "ver" are one verified club; ranking labels use the alias.
        Assert.False(await database.Clubs.AnyAsync(x => x.Id == "hel"));
        Assert.Equal("Verona", (await database.Clubs.SingleAsync(x => x.Id == "ver")).Name);
        var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == 13).OrderBy(x => x.Ordinal).ToArrayAsync();
        Assert.Equal(new[] { ("ver", "com"), ("juv", "ver"), ("ven", "com") }, matches.Select(x => (x.HomeClubId, x.AwayClubId)));
        // Official long names, with or without a code, become the canonical club.
        Assert.Equal(new[] { ("com", "Como"), ("ven", "Venezia") },
            (await database.Clubs.Where(x => x.Id == "com" || x.Id == "ven").OrderBy(x => x.Id).ToArrayAsync()).Select(x => (x.Id, x.Name)));
        Assert.Equal(ClubMembershipSource.Calendar | ClubMembershipSource.Standings, (await database.ClubSeasons.SingleAsync(x => x.ClubId == "ver" && x.SeasonIndex == 13)).Sources);
        Assert.Contains(report.ResolvedClubCodes!, x => x.Container == "realcalendar" && x.JsonPath == "$.v.d[0].g[0].h" && x.CatalogueVersion == FantacalcioClubCodes.Version);

        var current = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year).OrderBy(x => x.Ordinal).ToArrayAsync();
        Assert.Equal((2, 1), (current[0].HomeGoals, current[0].AwayGoals));
        Assert.Null(current[1].HomeGoals); Assert.Null(current[1].AwayGoals); Assert.NotNull(current[1].KickoffUtc);
        // The current provider labels Monza "MONZ": same verified club, reported.
        Assert.Equal("mon", current[1].AwayClubId); Assert.False(await database.Clubs.AnyAsync(x => x.Id == "monz"));
        Assert.Contains(report.ResolvedClubCodes!, x => x.DocumentSha256 == Hash(year) && x.JsonPath == "$.v.d[0].g[1].a");
        var future = Assert.Single(report.DiscardedFutureScores!);
        Assert.Equal((Hash(year), year, 1, "$.v.d[0].g[1]"), (future.DocumentSha256, future.SeasonIndex, future.Games, future.FirstJsonPath));
        Assert.Equal(manifest.StartedAtUtc, future.ExportStartedAtUtc);

        var serialized = JsonSerializer.Serialize(report);
        foreach (var label in new[] { "Como 1907", "Venezia FC", "Hellas" }) Assert.DoesNotContain(label, serialized);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task A_calendar_mixing_two_seasons_is_refused_instead_of_partially_imported()
    {
        await PutLegacyDocumentAsync("realcalendar", 11, new RealCalendar { Year = 11, Days =
        [
            new() { Year = 11, SerieADay = 1, Games = [CalendarGame("Milan", null, "Inter", null, new(2022, 8, 13, 18, 30, 0), 1, 1)] },
            new() { Year = 11, SerieADay = 2, Games = [CalendarGame("Roma", null, "Lazio", null, new(2023, 8, 20, 20, 45, 0))] }
        ] });
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("real_calendar_mixed_seasons", fault.Message); Assert.Equal("calendar", fault.Field);
        Assert.Equal(manifest.Containers.Single(x => x.Name == "realcalendar").Blobs.Single().Sha256, fault.DocumentSha256);
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.FootballMatches.ToArrayAsync());
    }

    [CoreFact]
    public async Task Calendars_without_a_stored_year_are_accepted_only_when_every_day_confirms_the_key()
    {
        // From 2023/24 the legacy calendars store year 0 while every day carries the key.
        RealDay Day(int year, int number, DateTime date) => new() { Year = year, SerieADay = number, Games = [CalendarGame("Milan", "mil", "Inter", "int", date, 1, 0)] };
        await PutLegacyDocumentAsync("realcalendar", 11, new RealCalendar { Year = 0, Days = [Day(11, 1, new(2022, 8, 14, 18, 30, 0)), Day(0, 2, new(2022, 8, 21, 18, 30, 0))] });
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var unconfirmed = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal(("calendar_season_mismatch", "realcalendar $.Value.y"), (unconfirmed.Message, unconfirmed.Field));
        await PutLegacyDocumentAsync("realcalendar", 11, new RealCalendar { Year = 10, Days = [Day(11, 1, new(2022, 8, 14, 18, 30, 0))] });
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var different = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("calendar_season_mismatch", different.Message);

        await PutLegacyDocumentAsync("realcalendar", 11, new RealCalendar { Year = 11, Days = [Day(11, 1, new(2022, 8, 14, 18, 30, 0))] });
        await PutLegacyDocumentAsync("realcalendar", 12, new RealCalendar { Year = 0, Days = [Day(12, 1, new(2024, 8, 18, 18, 30, 0))] });
        await PutLegacyDocumentAsync("realcalendar", 13, new RealCalendar { Year = 0, Days = [Day(13, 1, new(2024, 8, 17, 18, 30, 0)), Day(13, 2, new(2024, 8, 24, 18, 30, 0))] });
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        string Hash(int key) => manifest.Containers.Single(x => x.Name == "realcalendar").Blobs.Single(x => x.Name == key.ToString()).Sha256;
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(new[] { (Hash(12), 12, 1), (Hash(13), 13, 2) },
            report.RealCalendarsWithoutYear!.OrderBy(x => x.SeasonIndex).Select(x => (x.DocumentSha256, x.SeasonIndex, x.Days)));
        Assert.Contains(report.PendingFields, x => x.Contains("realCalendarsWithoutYear", StringComparison.Ordinal));
        // The usual season rules still apply: 12 holds 2024/25 games and stays excluded.
        Assert.Equal(12, Assert.Single(report.ExcludedRealCalendars!).SeasonIndex);
        Assert.Equal(new[] { (11, 1), (13, 1), (13, 2) }, (await database.FootballMatchdays.OrderBy(x => x.SeasonIndex).ThenBy(x => x.SerieADay).ToArrayAsync())
            .Select(x => (x.SeasonIndex, x.SerieADay)));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }
}
