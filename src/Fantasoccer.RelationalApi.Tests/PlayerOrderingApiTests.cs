using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Auction_order_is_scoped_versioned_and_does_not_change_global_catalogue_visibility()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, 23);
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Order club", Abbreviation = "ord" };
            database.Clubs.Add(club);
            for (var i = 0; i < 3; i++)
            {
                var player = new FootballPlayer { Name = "Order player " + Guid.NewGuid().ToString("N") };
                database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = 23, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, Position = PlayerPosition.Forward, IsActive = true, IsVisible = true });
            }
            await database.SaveChangesAsync();
        }
        var path = $"/api/competitions/{group.GroupId}/{league}/23/auction-order";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(path + "/randomize", new RandomizePlayerOrderRequest())).StatusCode);
        (await manager.PostAsJsonAsync(path + "/randomize", new RandomizePlayerOrderRequest())).EnsureSuccessStatusCode();
        var first = (await manager.GetFromJsonAsync<PlayerOrderDto>(path))!;
        Assert.Equal(1, first.Version);
        Assert.Equal(3, first.Players.Length);
        Assert.All(first.Players, x => Assert.False(x.IsVisible));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/randomize", new RandomizePlayerOrderRequest())).StatusCode);
        (await manager.PostAsJsonAsync(path + "/randomize", new RandomizePlayerOrderRequest(1))).EnsureSuccessStatusCode();
        var second = (await manager.GetFromJsonAsync<PlayerOrderDto>(path))!;
        Assert.Equal(2, second.Version);
        Assert.Equal(first.Players.Select(x => x.PlayerId).Order(), second.Players.Select(x => x.PlayerId).Order());
        await using var final = factory.Database.CreateContext();
        Assert.True(await final.PlayerSeasons.Where(x => x.SeasonIndex == 23).AllAsync(x => x.IsVisible));
    }
}
