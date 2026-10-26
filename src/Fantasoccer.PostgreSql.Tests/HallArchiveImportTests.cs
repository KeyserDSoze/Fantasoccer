using System.Text.Json;
using Fantasoccer.Application.Core.HallOfFame;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Hall_archive_preserves_stage_years_unknown_season_player_records_standalone_games_and_retired_owners()
    {
        var container = await ExtraContainerAsync("halloffame");
        var player = new Player { Name = "Archive player", RealTeam = new() { Name = "Archive club", Abbreviation = "arc" }, Role = Role.Forward,
            Price = 42, Revenue = 5, Position = FantaSoccerRole.Forward, IsActive = true };
        const string retired = "retired@example.test";
        var archive = new HallOfFame { RecordPlayer = new() { Player = player, Points = 18.5f }, PlayerWithMostPointsInYear = new() { Player = player, Points = 240.75f, Year = 2 },
            RecordGame = new() { Year = 12, Game = new() { Id = "standalone-record", Home = "Old home", HomeOwner = Email, Away = "Old away", AwayOwner = OtherEmail,
                Number = 3, Result = new() { Home = new(100.25f, true, false, false), Away = new(72.5f, false, true, false), HomeGoal = 5, AwayGoal = 2 } } },
            WinningPlayers = [new() { Player = player, Wins = new() { ["@"] = [1, 2, 2], ["Finals"] = [3] } }],
            WinningTeams = [new() { Owner = retired, Team = "Retired name", Wins = new() { ["@"] = [1, 2], ["Finals"] = [3] } }],
            Ranks = [new() { Owner = retired, Name = "Retired name", Point = 1000.5f, Money = 300, PlusMoney = 20, Goal = 50 }] };
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<HallOfFame, GeneralLeagueKey>.Default(archive, new() { Group = "first", League = "league" }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var saved = await database.HallArchives.AsSplitQuery().SingleAsync();
        Assert.Equal(2, saved.PlayerRecords.Count);
        Assert.Null(saved.PlayerRecords.Single(x => x.Kind == 0).Year);
        Assert.Equal(2, saved.PlayerRecords.Single(x => x.Kind == 1).Year);
        Assert.Null(saved.RecordGame!.LinkedMatchId);
        Assert.Equal("standalone-record", saved.RecordGame.LegacyGameId);
        Assert.Equal(100.25f, saved.RecordGame.Result!.Home!.Value);
        Assert.Equal(new[] { 1, 2, 2 }, saved.WinningPlayers.Single().Wins.Single(x => x.Stage == "@").Years.OrderBy(x => x.Ordinal).Select(x => x.Year));
        var historical = await database.Users.SingleAsync(x => x.Email == retired);
        Assert.False(historical.IsEnabled);
        Assert.True(historical.IsHistoricalPlaceholder);
        Assert.Equal(historical.Id, saved.Ranks.Single().OwnerUserId);
        Assert.Equal(1000.5f, saved.Ranks.Single().Points);
        Assert.Equal(0, await database.PlayerSeasons.CountAsync()); // No season was invented for unseasoned record snapshots.
        var group = saved.GroupId; var league = saved.LeagueId;
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var dto = (await scope.ServiceProvider.GetRequiredService<HallOfFameApplicationService>().GetAsync(Caller(), group, league))!;
        Assert.Equal(42, dto.RecordPlayer!.Player.Price);
        Assert.Equal(240.75f, dto.PlayerWithMostPointsInYear!.Points);
        Assert.False(dto.RecordGame!.IsLinkedToCalendar);
        Assert.Equal("Finals", dto.WinningTeams.Single().Wins.Keys.Last());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await container.DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.HallArchives.ToArrayAsync());
        Assert.False(await database.Users.AnyAsync(x => x.Email == retired));
    }
}
