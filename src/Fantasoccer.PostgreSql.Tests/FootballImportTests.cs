using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Football_samples_preserve_sources_legacy_chances_fractional_rank_and_history_and_reconcile_removals()
    {
        var club = new RealTeam { Name = "Sample club", Abbreviation = "sam" };
        var key = new YearDayKey { Year = 15, Day = 3 };
        var vote = new VotedRealPlayer { Name = "Sample player", Role = Role.Forward, RealTeam = club, IsActive = true,
            Vote = new() { Role = Role.Forward, Value = 7.5f, Goal = 2, IsFinal = true, HasVote = true, Injured = true } };
        foreach (var source in new[] { "live", "official", "notification" })
        {
            var container = await ExtraContainerAsync(source);
            await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(
                Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new() { Players = [vote] }, key))));
        }
        var chance = await ExtraContainerAsync("chancedrealplayerwrapper");
        await chance.GetBlobClient("sample").UploadAsync(BinaryData.FromString("""
            {"k":{"y":15,"d":3},"v":{"p":[{"n":"Sample player","t":{"n":"Sample club","a":"sam"},"r":3,"a":true,
            "c":{"f":true,"g":false,"m":true,"s":false,"t":4,"d":"Doubtful","r":3,"l":{"d":2,"v":6.5,"p":1}}}]}}
            """));
        var statistics = await ExtraContainerAsync("statplayerswrapper");
        await statistics.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<StatPlayersWrapper, int>.Default(new()
        { Players = [new() { Name = vote.Name, RealTeam = club, Role = Role.Forward, Summatory = 13.5f, FantaSummatory = 19.5f,
            WithVote = 2, AllGames = [new() { SerieADay = 2, Vote = 6, Positiveness = -1 }, new() { SerieADay = 3, Vote = 7.5f, Positiveness = 1 }],
            Description = new() { Top = "Strength", Down = "Weakness" } }] }, 15))));
        var rank = await ExtraContainerAsync("realrank");
        await rank.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealRank, int>.Default(new()
        { Teams = [new() { Name = club.Name, Point = 4.5f, Victories = 1, Draws = 1, Played = 2, Goal = 3, SufferedGoal = 1 }] }, 15))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        Assert.Single(await database.Players.ToArrayAsync());
        Assert.Equal(3, await database.FootballVotes.CountAsync());
        Assert.Equal(3, await database.FootballVotes.Select(x => x.Source).Distinct().CountAsync());
        var storedChance = await database.FootballChances.SingleAsync();
        Assert.Equal(6.5f, storedChance.LegacyLastGameVote);
        Assert.Equal(3, storedChance.LegacyTrend);
        var storedStats = await database.FootballPlayerStatistics.SingleAsync();
        Assert.Equal(new[] { 2, 3 }, storedStats.Games.OrderBy(x => x.Ordinal).Select(x => x.Day));
        Assert.Equal("Strength", storedStats.DescriptionTop);
        Assert.Equal(4.5f, (await database.FootballClubRanks.SingleAsync()).Points);
        Assert.Equal("sam", (await database.Clubs.SingleAsync()).Id);
        Assert.Equal("sam", (await database.FootballClubRanks.SingleAsync()).ClubId);
        Assert.Equal(storedChance.PlayerId, storedStats.PlayerId);
        var ids = await database.Players.Select(x => x.Id).ToArrayAsync();
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await _storage.Blobs.GetBlobContainerClient("live").DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(2, await database.FootballVotes.CountAsync());
        Assert.Equal(ids, await database.Players.Select(x => x.Id).ToArrayAsync());
        Assert.False(await database.FootballDatasets.AnyAsync(x => x.Kind == "live"));
    }
}
