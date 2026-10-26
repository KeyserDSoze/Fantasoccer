using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.HallOfFame;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Hall_HTTP_reads_descriptive_historical_DTOs_and_does_not_accept_a_league_from_another_group()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner);
        var second = await CreateGroupAsync(owner);
        var league = await CreateLeagueAsync(owner, group.GroupId, 24);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        await using (var database = factory.Database.CreateContext())
        {
            var player = new FootballPlayer { Name = "Hall HTTP player " + Guid.NewGuid().ToString("N") };
            var club = new FootballClub { Name = "Hall HTTP club", Abbreviation = "hhc" };
            database.Players.Add(player); database.Clubs.Add(club);
            await database.SaveChangesAsync();
            database.HallArchives.Add(new() { GroupId = group.GroupId, LeagueId = league,
                PlayerRecords = [new() { Kind = 0, Points = 17.5f, Player = new() { PlayerId = player.Id, ClubId = club.Id, Name = "Historical displayed name", Price = 42, PlayerRole = 3, Position = 3 } }],
                WinningTeams = [new() { OwnerUserId = account.Id, OwnerEmail = account.Email, Name = "Historical team", Wins =
                    [new() { Stage = "Finals", Years = [new() { Year = 24 }] }] }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/leagues/{league}/hall-of-fame";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/groups/{second.GroupId}/leagues/{league}/hall-of-fame")).StatusCode);
        var json = await owner.GetFromJsonAsync<JsonElement>(path);
        Assert.True(json.TryGetProperty("recordPlayer", out var record));
        Assert.Equal(42, record.GetProperty("player").GetProperty("price").GetInt32());
        Assert.False(json.TryGetProperty("rp", out _));
        var dto = (await owner.GetFromJsonAsync<HallOfFameDto>(path))!;
        Assert.Equal("Historical displayed name", dto.RecordPlayer!.Player.Name);
        Assert.Equal(24, dto.WinningTeams.Single().Wins["Finals"].Single());
    }
}
