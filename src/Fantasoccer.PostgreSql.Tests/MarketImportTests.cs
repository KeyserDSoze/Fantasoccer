using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Market_import_preserves_proposals_statuses_votes_prices_and_dates_without_executing_trades()
    {
        _first.Baskets[0].Years[0].Teams[0].AdditionalOwners = []; // one team per user and league
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Other team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        var container = await ExtraContainerAsync("marketwrapper");
        var key = new LeagueKey { Group = "first", League = "league", Year = 15 };
        Player Player(string name, int price) => new() { Name = name, RealTeam = new() { Name = "Market club" }, Role = Role.Forward, Price = price,
            Revenue = 4, Position = FantaSoccerRole.Forward, IsActive = true };
        var created = new DateTime(2026, 10, 20, 10, 30, 0, DateTimeKind.Utc);
        var wrapper = new MarketWrapper { Markets = [new() { Id = "pending-trade", Buyer = Email, Seller = OtherEmail,
            BuyerPlayers = [Player("Buyer player", 30)], SellerPlayers = [Player("Seller player", 70)], MoneyFromBuyer = 15, MoneyFromSeller = 2,
            SellerAccepted = false, Approvers = [Email], Deniers = [], CreationTime = created },
            new() { Id = "completed-trade", Buyer = Email, Seller = OtherEmail, BuyerPlayers = [Player("Buyer player", 30)], SellerPlayers = [Player("Seller player", 70)],
                Approvers = [Email, OtherEmail], Deniers = [], SellerAccepted = true, Status = MarketStatus.Approved, CreationTime = created }] };
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<MarketWrapper, LeagueKey>.Default(wrapper, key))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var trades = await database.MarketTrades.AsSplitQuery().OrderBy(x => x.Ordinal).ToArrayAsync();
        Assert.Equal(2, trades.Length);
        Assert.False(trades[0].SellerAccepted);
        Assert.Equal(15, trades[0].MoneyFromBuyer);
        Assert.Equal(30, trades[0].Players.Single(x => x.FromBuyer).Price);
        Assert.Equal(70, trades[0].Players.Single(x => !x.FromBuyer).Price);
        Assert.Single(trades[0].Votes);
        Assert.Equal(1, trades[1].Status);
        Assert.Equal(2, trades[1].Votes.Count);
        Assert.Equal(new DateTimeOffset(created), trades[0].CreatedAtUtc);
        Assert.Empty(await database.RosterSnapshots.ToArrayAsync());
        Assert.Empty(await database.MarketTradeEvents.ToArrayAsync());
        var ids = trades.Select(x => x.Id).ToArray();
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        wrapper.Markets.RemoveAt(1);
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<MarketWrapper, LeagueKey>.Default(wrapper, key))), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(ids[0], (await database.MarketTrades.SingleAsync()).Id);
        Assert.Equal(2, await database.Players.CountAsync());
    }
}
