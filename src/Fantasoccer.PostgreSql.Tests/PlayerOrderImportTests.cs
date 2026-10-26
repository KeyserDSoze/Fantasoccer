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
    public async Task League_player_order_preserves_order_and_visibility_without_duplicating_shared_players()
    {
        var container = await ExtraContainerAsync("randomizedplayers");
        var players = new[]
        {
            new RealPlayer { Name = "Second in alphabet", Role = Role.Forward, RealTeam = new() { Name = "Club" }, Visible = false, IsActive = true },
            new RealPlayer { Name = "First in alphabet", Role = Role.Defensor, RealTeam = new() { Name = "Club" }, Visible = true, IsActive = true }
        };
        foreach (var group in new[] { "first", "second" })
            await container.GetBlobClient(group).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealPlayersWrapper, LeagueKey>.Default(
                new() { Players = players.ToList() }, new() { Group = group, League = "league", Year = 15 }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        Assert.Equal(2, await database.Players.CountAsync());
        Assert.Equal(2, await database.LeaguePlayerOrders.CountAsync());
        var orders = await database.LeaguePlayerOrders.Include(x => x.Players).ThenInclude(x => x.Player).AsSplitQuery().ToArrayAsync();
        foreach (var order in orders)
        {
            var rows = order.Players.OrderBy(x => x.Ordinal).ToArray();
            Assert.Equal(players.Select(x => x.Name), rows.Select(x => x.Player.Name));
            Assert.False(rows[0].IsVisible);
            Assert.True(rows[1].IsVisible);
        }
        var ids = orders[0].Players.OrderBy(x => x.Ordinal).Select(x => x.PlayerId).ToArray();
        Assert.Equal(ids, orders[1].Players.OrderBy(x => x.Ordinal).Select(x => x.PlayerId));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await container.DeleteBlobAsync("second");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(1, await database.LeaguePlayerOrders.CountAsync());
        Assert.Equal(2, await database.Players.CountAsync());
    }
}
