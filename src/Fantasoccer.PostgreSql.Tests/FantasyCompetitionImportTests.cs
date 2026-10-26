using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Fantasy_import_keeps_stage_turn_matchday_results_and_rank_snapshots_distinct()
    {
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Second team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        var container = await ExtraContainerAsync("calendar");
        var key = new LeagueKey { Group = "first", League = "league", Year = 15 };
        var result = new GameResult { Home = new(71.5f, true, false, false), Away = new(67, false, true, false), HomeGoal = 2, AwayGoal = 1 };
        var calendar = new Calendar { Year = 15, Rounds = new() { ["Final stage"] = [new() { Number = 7, SerieADay = 12,
            Games = [new() { Id = "legacy-match", Number = 2, Home = "Historical name", HomeOwner = Email,
                Away = "Second team", AwayOwner = OtherEmail, Result = result }] }], ["Empty stage"] = [] } };
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Calendar, LeagueKey>.Default(calendar, key))));
        var ranks = await ExtraContainerAsync("rank");
        Rank Rank(float points) => new() { SerieADay = 12, RankedTeams = new() { ["Final stage"] =
            [new() { Name = "Historical name", Owner = Email, Point = points, Money = 123, PlusMoney = 8, ValuePoint = 711.5f }], ["Empty stage"] = [] } };
        await ranks.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Rank, LeagueKey>.Default(Rank(20.5f), key))));
        var daily = await ExtraContainerAsync("dailyrank");
        await daily.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Rank, YearDayLeague>.Default(Rank(3),
            new() { Group = key.Group, League = key.League, Year = key.Year, Day = 12 }))));
        var played = await ExtraContainerAsync("playedgame");
        await played.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<PlayedGame, GameKey>.Default(
            new() { Id = "legacy-match", Home = "Saved home", Away = "Saved away", Result = result,
                HomePlayers = [new() { Name = "Saved player", Role = Role.Forward, RealTeam = new() { Name = "Club" }, Price = 35, Position = FantaSoccerRole.Forward }], AwayPlayers = [] },
            new() { Group = key.Group, League = key.League, Year = key.Year, Day = 12, GameId = "legacy-match" }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var match = await database.FantasyMatches.Include(x => x.Matchday).SingleAsync();
        Assert.Equal(7, match.Matchday.FantasyTurn);
        Assert.Equal(12, match.Matchday.SerieADay);
        Assert.Equal("Final stage", match.Stage);
        Assert.Equal(71.5f, match.Result!.Home!.Value);
        Assert.True(match.Result.Home.DefensiveBonus);
        Assert.Equal(2, match.Result.HomeGoals);
        var snapshots = await database.FantasyStandingSnapshots.AsSplitQuery().OrderBy(x => x.SnapshotDay).ToArrayAsync();
        Assert.Equal(2, snapshots.Length);
        Assert.Equal(20.5f, snapshots[0].Stages.Single(x => x.Stage == "Final stage").Teams.Single().Points);
        Assert.Equal(3, snapshots[1].Stages.Single(x => x.Stage == "Final stage").Teams.Single().Points);
        Assert.Empty(snapshots[0].Stages.Single(x => x.Stage == "Empty stage").Teams);
        var saved = await database.PlayedMatchSnapshots.SingleAsync();
        Assert.Equal(match.Id, saved.MatchId);
        Assert.Equal("Saved home", saved.HomeName);
        Assert.Equal(35, Assert.Single(saved.Players).Price);
        Assert.Equal(71.5f, saved.Result!.Home!.Value);
        var id = match.Id;
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await played.DeleteBlobAsync("sample");
        calendar.Rounds["Final stage"][0].Games[0].Result!.HomeGoal = 5;
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Calendar, LeagueKey>.Default(calendar, key))), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(id, (await database.FantasyMatches.SingleAsync()).Id);
        Assert.Equal(5, (await database.FantasyMatches.SingleAsync()).Result!.HomeGoals);
        Assert.Empty(await database.PlayedMatchSnapshots.ToArrayAsync());
    }
}
