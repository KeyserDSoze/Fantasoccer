using System.Security.Cryptography;
using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Domain;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Explicit_auction_exclusion_skips_decoding_old_sessions_and_orders_without_keys_and_preserves_rosters()
    {
        foreach (var name in new[] { "auction", "randomizedplayers" })
        {
            var old = await ExtraContainerAsync(name);
            await old.GetBlobClient("old-archive").UploadAsync(BinaryData.FromString("Old unusable auction payload, intentionally not decoded."));
        }
        var club = new RealTeam { Name = "Retained club" };
        var player = new RealPlayer { Name = "Retained player", RealTeam = club, Role = Role.Forward, IsActive = true };
        var catalogue = await ExtraContainerAsync("realplayerswrapper");
        await catalogue.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<RealPlayersWrapper, int>.Default(new() { Players = [player] }, 15))));
        var teams = await ExtraContainerAsync("team");
        await teams.GetBlobClient("roster").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Team, TeamKey>.Default(new()
        {
            Name = "Team", Owner = Email, AdditionalOwners = [OtherEmail], MoneyFromRank = 17, OpeningCompetitionPrize = 9,
            Players = [new() { Name = player.Name, RealTeam = club, Role = Role.Forward, IsActive = true, Price = 37, Position = FantaSoccerRole.Forward }]
        }, new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var options = new CoreImportOptions(ImportLegacyAuctions: false);
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump, options: options);
        Assert.DoesNotContain(dataset.Documents, x => x.Container is "auction" or "randomizedplayers");
        Assert.Empty(plan.Auctions); Assert.Empty(plan.PlayerOrders);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(new[] { "auction", "dailyrank", "randomizedplayers", "table:logs" }, report.ExcludedContainers);
        Assert.DoesNotContain("auction", report.PendingContainers);
        Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("auction: excluded"));
        Assert.Contains(report.PreservedArtifacts!, x => x.StartsWith("randomizedplayers: excluded"));
        Assert.Empty(await database.AuctionSessions.ToArrayAsync()); Assert.Empty(await database.LeaguePlayerOrders.ToArrayAsync());
        var roster = await database.RosterSnapshots.SingleAsync();
        Assert.Equal(37, Assert.Single(roster.Players).Price); Assert.Equal(17, roster.MoneyFromRank); Assert.Equal(9, roster.OpeningCompetitionPrize);
        Assert.Equal(2, await database.Groups.CountAsync()); Assert.Equal(4, await database.LeagueRules.CountAsync());
        var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        var manifest = await DumpArchive.VerifyAsync(_dump);
        Assert.Single(manifest.Containers.Single(x => x.Name == "auction").Blobs);
        Assert.Single(manifest.Containers.Single(x => x.Name == "randomizedplayers").Blobs);
    }

    [CoreFact]
    public async Task Auction_import_requires_explicit_legacy_keys_and_preserves_order_current_statistics_timers_and_audit_without_live_presence()
    {
        _first.Baskets[0].Years[0].Teams[0].AdditionalOwners = []; // one team per user and league
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Other team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        var players = await ExtraContainerAsync("realplayerswrapper");
        var first = new RealPlayer { Name = "Auction forward", RealTeam = new() { Name = "Auction club" }, Role = Role.Forward, IsActive = true };
        var second = new RealPlayer { Name = "Auction defender", RealTeam = first.RealTeam, Role = Role.Defensor, IsActive = true };
        await players.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<RealPlayersWrapper, int>.Default(new() { Players = [first, second] }, 15))));
        using var aes = Aes.Create();
        aes.GenerateKey(); aes.GenerateIV();
        string Encrypt(string key)
        {
            using var output = new MemoryStream();
            using (var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
            using (var writer = new StreamWriter(crypto)) writer.Write(key);
            return Convert.ToBase64String(output.ToArray());
        }
        var options = new CoreImportOptions(Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
        var container = await ExtraContainerAsync("auction");
        var started = new DateTime(2026, 10, 20, 10, 30, 0, DateTimeKind.Utc);
        var auction = new AuctionWrapper { Id = "legacy-auction", Creator = Email, Created = started, LeagueKey = new() { Group = "first", League = "league", Year = 15 },
            Status = AuctionStatus.InProgress, Kind = AuctionKind.Repairing, Type = AuctionType.RandomList, CurrentRole = Role.Forward,
            LastShow = new() { [Role.GoalKeeper] = null, [Role.Forward] = first.Key },
            ListOfPlayers = new() { [Role.Forward] = [new() { EncryptedKey = Encrypt(first.Key), IsShowed = false }], [Role.Defensor] = [new() { EncryptedKey = Encrypt(second.Key), IsShowed = true }] },
            Actual = new() { Player = new() { Name = first.Name, RealTeam = first.RealTeam, Role = first.Role, Summatory = 13.5f, FantaSummatory = 16,
                WithVote = 2, Description = new() { Top = "Saved description" }, AllGames = [new() { SerieADay = 1, Vote = 7 }, new() { SerieADay = 2, Vote = 6.5f }] },
                Started = started, StartingTime = started.AddSeconds(5), Owner = Email, Price = 30, Substituted = second.Key },
            Winnings = [new() { PlayerKey = second.Key, Owner = OtherEmail, Price = 40 }],
            Participants = [new() { Name = "Historical participant", Owner = Email, AdditionalOwners = [OtherEmail] }] };
        await container.GetBlobClient(auction.Id).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<AuctionWrapper, string>.Default(auction, auction.Id))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var missing = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("legacy_auction_encryption_required", missing.Message);
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.AuctionSessions.ToArrayAsync());
        await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        var saved = await database.AuctionSessions.AsSplitQuery().SingleAsync();
        Assert.Equal(2, saved.PlayerOrder.Count);
        Assert.False(saved.PlayerOrder.Single(x => x.Role == 3).IsShown);
        Assert.True(saved.PlayerOrder.Single(x => x.Role == 1).IsShown);
        Assert.Null(saved.LastShown.Single(x => x.Role == 0).PlayerId);
        Assert.Equal(13.5f, saved.Actual!.Statistics.Summatory);
        Assert.Equal(new[] { 1, 2 }, saved.Actual.Statistics.Games.OrderBy(x => x.Ordinal).Select(x => x.Day));
        Assert.Equal("Saved description", saved.Actual.Statistics.DescriptionTop);
        Assert.Equal(new DateTimeOffset(started.AddSeconds(5)), saved.Actual.TimerStartedAtUtc);
        Assert.Equal(30, saved.Actual.Price);
        Assert.Equal(40, Assert.Single(saved.Winnings).Price);
        Assert.Equal(OtherEmail, Assert.Single(Assert.Single(saved.LegacyParticipants).CoOwners).Email);
        Assert.Empty(await database.AuctionPresence.ToArrayAsync());
        Assert.Equal(2, await database.Players.CountAsync());
        var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        // Switching this option must reconcile the target, not incorrectly take
        // the unchanged-snapshot fast path and keep previously imported sessions.
        var excluded = await new CoreDatasetImporter(database, new(ImportLegacyAuctions: false)).ImportAsync(_dump);
        Assert.Equal(0, excluded.SkippedDocuments); Assert.Contains("auction", excluded.ExcludedContainers!);
        Assert.Empty(await database.AuctionSessions.ToArrayAsync());
        Assert.Equal(2, await database.Players.CountAsync());
        await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Single(await database.AuctionSessions.ToArrayAsync());
        await container.DeleteBlobAsync(auction.Id);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Empty(await database.AuctionSessions.ToArrayAsync());
    }
}
