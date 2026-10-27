using System.Text.Json;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Club_seasons_record_only_attested_membership_while_global_names_and_player_links_are_canonical()
    {
        async Task Put(string containerName, string name, object key, object value)
        {
            var container = _extraContainers.Contains(containerName) ? _storage.Blobs.GetBlobContainerClient(containerName) : await ExtraContainerAsync(containerName);
            await container.GetBlobClient(name).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { Key = key, Value = value })), overwrite: true);
        }
        await Put("realteamwrapper", "1", 1, new { t = new[] { new { n = "Juve", a = (string?)null }, new { n = "FIORENTINA", a = (string?)null } } });
        await Put("realteamwrapper", "2", 2, new { t = new[] { new { n = "JUVENTUS", a = (string?)null }, new { n = "napoli", a = (string?)null } } });
        await Put("realteamwrapper", "3", 3, new { t = Array.Empty<object>() });
        foreach (var (year, club) in new[] { (1, "FIORENTINA"), (2, "Juve") })
            await Put("realplayerswrapper", year.ToString(), year, new RealPlayersWrapper { Players = [new()
                { Name = "Transfer fixture player", Role = Role.Forward, RealTeam = new() { Name = club, Abbreviation = "" }, IsActive = true }] });
        await Put("official", "source", new YearDayKey { Year = 2, Day = 1 }, new VotedRealPlayerWrapper { Players = [new()
            { Name = "Transfer fixture player", Role = Role.Forward, RealTeam = new() { Name = "JUVENTUS", Abbreviation = "" }, Vote = new() { Value = 7, HasVote = true } }] });
        await Put("realcalendar", "2", 2, new RealCalendar { Year = 2, Days = [new() { Year = 2, SerieADay = 1, Games =
            [new() { Home = new() { Name = "juv" }, Away = new() { Name = "NAPOLI", Abbreviation = "" } }] }] });
        await Put("realrank", "2", 2, new RealRank { Teams = [new() { Name = "Juve", Point = 3 }, new() { Name = "napoli", Point = 0 }] });
        await Put("team", "history", new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }, new Team
            { Name = "Frozen historical roster", Owner = Email, MoneyFromRank = 17, Players = [new() { Name = "Historical player", Role = Role.Forward,
                RealTeam = new() { Name = "CATANIA", Abbreviation = "" }, Price = 41, Revenue = 58, Status = PlayerInTeamStatus.Sold, Position = FantaSoccerRole.Tribune }] });
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(4, report.Clubs); Assert.Equal(4, report.ClubSeasons); Assert.Equal(new[] { 1, 2, 3 }, report.ClubCatalogueYears);
        Assert.Equal(new[] { "Catania", "Fiorentina", "Juventus", "Napoli" }, await database.Clubs.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(new[] { "fio:1", "juv:1", "juv:2", "nap:2" }, (await database.ClubSeasons.ToArrayAsync()).Select(x => x.ClubId + ":" + x.SeasonIndex).Order());
        Assert.All(await database.ClubSeasons.Where(x => x.SeasonIndex == 2).ToArrayAsync(),
            x => Assert.Equal(ClubMembershipSource.Catalogue | ClubMembershipSource.Calendar | ClubMembershipSource.Standings, x.Sources));
        Assert.False(await database.ClubSeasons.AnyAsync(x => x.ClubId == "cat"));
        Assert.Equal("cat", (await database.PlayerSeasons.SingleAsync(x => x.SeasonIndex == 15)).ClubId);
        Assert.Empty((await new SqlFootballIngestionReader(database).ClubsAsync(15))!.Teams);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        var key = KeyUtils.GetPlayerKey("Transfer fixture player");
        var current = await database.PlayerSeasons.SingleAsync(x => x.PlayerId == key && x.SeasonIndex == 2);
        current.ClubId = "nap";
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();
        Assert.Equal("nap", (await database.PlayerSeasons.SingleAsync(x => x.PlayerId == key && x.SeasonIndex == 2)).ClubId);
        Assert.Equal("fio", (await database.PlayerSeasons.SingleAsync(x => x.PlayerId == key && x.SeasonIndex == 1)).ClubId);
        Assert.Equal("juv", (await database.FootballVotes.SingleAsync()).ClubId);
        var historical = (await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync()).Players.Single();
        Assert.Equal("cat", historical.ClubId); Assert.Equal(41, historical.Price); Assert.Equal(58, historical.Revenue);
        Assert.Equal(4, await database.ClubSeasons.CountAsync());
    }
}

public sealed class ClubSeasonModelTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Canonical_club_names_are_enforced_on_sync_async_writes_and_direct_SQL_without_changing_keys()
    {
        await using var database = fixture.CreateContext(); await using var transaction = await database.Database.BeginTransactionAsync();
        var club = new FootballClub { Name = "JUVE", Abbreviation = "JUV" }; database.Clubs.Add(club);
        await database.SaveChangesAsync(); Assert.Equal("juv", club.Id); Assert.Equal("Juventus", club.Name); Assert.Equal("juv", club.Abbreviation);
        club.Name = "juve"; club.Abbreviation = "JUV"; database.SaveChanges();
        Assert.Equal("Juventus", await database.Clubs.AsNoTracking().Where(x => x.Id == "juv").Select(x => x.Name).SingleAsync());
        club.Name = "Napoli";
        Assert.Throws<InvalidOperationException>(() => database.SaveChanges());
        club.Name = "Juventus"; club.Abbreviation = "NAP";
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
        club.Abbreviation = "juv";
        foreach (var (field, value) in new[] { ("Name", "Juve"), ("Abbreviation", "JUV") })
        {
            await transaction.CreateSavepointAsync("invalid_club");
            var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(
                "UPDATE \"Clubs\" SET \"" + field + "\"={0} WHERE \"ClubCode\"='juv'", value));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            await transaction.RollbackToSavepointAsync("invalid_club");
        }
        Assert.Equal("juv", (await database.Clubs.AsNoTracking().SingleAsync()).Id);
    }

    [PostgreSqlFact]
    public async Task Club_season_primary_and_foreign_keys_are_code_and_year_not_numeric_club_IDs()
    {
        await using var database = fixture.CreateContext(); await using var transaction = await database.Database.BeginTransactionAsync();
        var club = new FootballClub { Name = "Juve" };
        database.ClubSeasons.AddRange(new() { Club = club, Season = new() { SeasonIndex = 1, StartYear = 2012 }, Sources = ClubMembershipSource.Catalogue },
            new() { Club = club, Season = new() { SeasonIndex = 2, StartYear = 2013 }, Sources = ClubMembershipSource.Calendar });
        await database.SaveChangesAsync(); Assert.Equal(2, await database.ClubSeasons.CountAsync());
        var key = database.Model.FindEntityType(typeof(FootballClubSeason))!.FindPrimaryKey()!;
        Assert.Equal(new[] { "ClubId", "SeasonIndex" }, key.Properties.Select(x => x.Name));
        Assert.Equal(typeof(string), key.Properties[0].ClrType);
        foreach (var (code, year, sources, expected) in new[]
        {
            ("juv", 1, 1, PostgresErrorCodes.UniqueViolation),
            ("xyz", 1, 1, PostgresErrorCodes.ForeignKeyViolation),
            ("juv", 99, 1, PostgresErrorCodes.ForeignKeyViolation),
            ("juv", 1, 0, PostgresErrorCodes.CheckViolation)
        })
        {
            await transaction.CreateSavepointAsync("invalid_membership");
            var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"ClubSeasons\" (\"ClubCode\",\"SeasonIndex\",\"Sources\") VALUES ({code},{year},{sources})"));
            Assert.Equal(expected, failure.SqlState);
            await transaction.RollbackToSavepointAsync("invalid_membership");
        }
    }
}
