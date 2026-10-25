using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

public sealed partial class McpApiTests
{
    [Theory]
    [InlineData("api/Team/Get")]
    [InlineData("api/Rank/Get")]
    [InlineData("api/Rank/Exist")]
    [InlineData("api/Rank/day/Get")]
    [InlineData("api/Calendar/Get")]
    [InlineData("api/Team/day/Get")]
    [InlineData("api/Team/opening/Get")]
    [InlineData("api/Team/openingdaily/Get")]
    public async Task Composite_JSON_keys_read_the_real_roster_and_standings_through_MCP(string path)
    {
        var access = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var seed = await SeedCompositeKeyDataAsync(Email);
        object key = path switch
        {
            "api/Rank/day/Get" => new YearDayLeague { Group = seed.Group, League = "league", Year = 15, Day = 3 },
            "api/Team/day/Get" => new TeamDayKey { Group = seed.Group, Year = 15, BasketId = "basket", Email = Email, Day = 3 },
            "api/Team/opening/Get" => new OpeningTeamKey { Group = seed.Group, League = "league", Year = 15, BasketId = "basket", Email = Email },
            "api/Team/openingdaily/Get" => new OpeningTeamDayKey { Group = seed.Group, League = "league", Year = 15, BasketId = "basket", Email = Email, Day = 3 },
            "api/Team/Get" => new TeamKey { Group = seed.Group, Year = 15, BasketId = "basket", Email = Email },
            _ => new LeagueKey { Group = seed.Group, League = "league", Year = 15 }
        };
        var result = await CallAsync(access, "execute_api_operation", new
        {
            operation = Find(path), arguments = new { query = new { key = JsonSerializer.Serialize(key) } }
        });
        Assert.False(result.GetProperty("isError").GetBoolean(), result.GetRawText());
        var payload = JsonSerializer.Deserialize<JsonElement>(result.GetProperty("content")[0].GetProperty("text").GetString()!);
        Assert.Equal(200, payload.GetProperty("statusCode").GetInt32());
        var body = JsonSerializer.Deserialize<JsonElement>(payload.GetProperty("body").GetString()!);
        if (path.EndsWith("/Exist")) Assert.True(body.GetProperty("i").GetBoolean());
        else if (path.StartsWith("api/Rank/")) Assert.Contains(seed.TeamName, body.GetRawText());
        else if (path.StartsWith("api/Team/")) Assert.Equal(seed.TeamName, body.GetProperty("n").GetString());
        else Assert.Equal(15, body.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task Typed_key_normalization_preserves_permissions_for_members_and_group_admins()
    {
        var access = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var seed = await SeedCompositeKeyDataAsync(Email);
        var key = new TeamKey { Group = seed.Group, Year = 15, BasketId = "basket", Email = Email };
        // Full CLR field names and case-insensitive query names normalize to the same
        // compact typed key that the guard checks and the endpoint actually reads.
        var fullNames = JsonSerializer.Serialize(new { Group = key.Group, Year = "15", BasketId = key.BasketId, Email = key.Email });
        var read = await CallAsync(access, Find("api/Team/Get"), new { query = new { KEY = fullNames } });
        Assert.False(read.GetProperty("isError").GetBoolean(), read.GetRawText());
        Assert.Contains(seed.TeamName, read.GetProperty("content")[0].GetProperty("text").GetString());

        var forged = new Team { Name = "Forbidden replacement", Owner = Email, AdditionalOwners = [], Players = [], MoneyFromRank = 9999 };
        var denied = await CallAsync(access, Find("api/Team/Update"), new { query = new { key = JsonSerializer.Serialize(key) }, body = forged });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        Assert.Contains("Forbidden", denied.GetProperty("content")[0].GetProperty("text").GetString());
        key.Group = "another-group";
        denied = await CallAsync(access, Find("api/Team/Get"), new { query = new { key = JsonSerializer.Serialize(key) } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        Assert.Contains("Forbidden", denied.GetProperty("content")[0].GetProperty("text").GetString());

        var admin = "mcp-key-admin-" + Guid.NewGuid().ToString("N") + "@example.com";
        var adminAccess = (await ConnectAsync(admin)).GetProperty("access_token").GetString()!;
        var adminSeed = await SeedCompositeKeyDataAsync(admin, IdentityRole.Admin);
        var adminKey = new TeamKey { Group = adminSeed.Group, Year = 15, BasketId = "basket", Email = admin };
        var replacement = new Team { Name = "Authorized update", Owner = admin, AdditionalOwners = [], Players = [], MoneyFromRank = 42 };
        var updated = await CallAsync(adminAccess, Find("api/Team/Update"), new { query = new { key = JsonSerializer.Serialize(adminKey) }, body = replacement });
        Assert.False(updated.GetProperty("isError").GetBoolean(), updated.GetRawText());
        var teams = _factory.Services.GetRequiredService<IRepository<Team, TeamKey>>();
        var stored = await teams.GetAsync(adminKey, TestContext.Current.CancellationToken);
        Assert.True(stored != null, updated.GetRawText());
        Assert.Equal("Authorized update", stored.Name);
        Assert.Equal(seed.TeamName, (await teams.GetAsync(new() { Group = seed.Group, Year = 15, BasketId = "basket", Email = Email }, TestContext.Current.CancellationToken))!.Name);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("group-id|||15|||basket|||owner@example.com")]
    [InlineData("{\"g\":\"group-id\",\"y\":15,\"b\":\"basket\"}")]
    [InlineData("{\"g\":\"group-id\",\"y\":15,\"b\":\"basket\",\"e\":\"owner@example.com\",\"Group\":\"other-group\"}")]
    [InlineData("{\"g\":\"group-id\",\"g\":\"group-id\",\"y\":15,\"b\":\"basket\",\"e\":\"owner@example.com\"}")]
    [InlineData("{\"g\":\"group-id\",\"y\":15,\"b\":\"basket\",\"e\":\"owner@example.com\",\"extra\":true}")]
    [InlineData("{\"g\":\"group-id\",\"y\":15,\"b\":\"basket\",\"e\":\"owner|||other@example.com\"}")]
    public async Task Invalid_composite_keys_are_clear_tool_errors_even_for_global_admins(string key)
    {
        var access = (await ConnectAsync("alessandro.rapiti44@gmail.com")).GetProperty("access_token").GetString()!;
        var result = await CallAsync(access, Find("api/Team/Get"), new { query = new { key } });
        Assert.True(result.GetProperty("isError").GetBoolean());
        var error = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.StartsWith("Invalid repository key.", error);
        Assert.DoesNotContain("Operation failed", error);
        Assert.DoesNotContain("owner@example.com", error);
        Assert.DoesNotContain("IndexOutOfRange", error);
    }

    private async Task<(string Group, string TeamName)> SeedCompositeKeyDataAsync(string email, IdentityRole role = IdentityRole.Participant)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = "mcp-keys-" + Guid.NewGuid().ToString("N");
        var name = "Trusted roster " + Guid.NewGuid().ToString("N");
        await _factory.Services.GetRequiredService<IRepository<Group, string>>().InsertAsync(group, new Group
        {
            Id = group, Name = "Composite keys", Users = [new() { Email = email, Role = role }],
            Leagues = [new() { Id = "league", Name = "League", Type = LeagueType.League, IsMain = true, BasketsId = ["basket"],
                Years = [new() { Year = 15, Settings = LeagueSetting.Default }] }],
            Baskets = [new() { Id = "basket", Name = "Basket", Years = [new() { Year = 15,
                Teams = [new() { Owner = email, Name = name, AdditionalOwners = [] }] }] }]
        }, cancellationToken);
        var team = new Team { Name = name, Owner = email, AdditionalOwners = [], Players = [], MoneyFromRank = 42 };
        await _factory.Services.GetRequiredService<IRepository<Team, TeamKey>>().InsertAsync(
            new() { Group = group, Year = 15, BasketId = "basket", Email = email }, team, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Team, TeamDayKey>>().InsertAsync(
            new() { Group = group, Year = 15, BasketId = "basket", Email = email, Day = 3 }, team, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Team, OpeningTeamKey>>().InsertAsync(
            new() { Group = group, League = "league", Year = 15, BasketId = "basket", Email = email }, team, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Team, OpeningTeamDayKey>>().InsertAsync(
            new() { Group = group, League = "league", Year = 15, BasketId = "basket", Email = email, Day = 3 }, team, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Rank, LeagueKey>>().InsertAsync(
            new() { Group = group, League = "league", Year = 15 },
            new Rank { SerieADay = 3, RankedTeams = new() { ["round"] = [new() { Name = name, Owner = email, Point = 9 }] } }, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Rank, YearDayLeague>>().InsertAsync(
            new() { Group = group, League = "league", Year = 15, Day = 3 },
            new Rank { SerieADay = 3, RankedTeams = new() { ["round"] = [new() { Name = name, Owner = email, Point = 3 }] } }, cancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Calendar, LeagueKey>>().InsertAsync(
            new() { Group = group, League = "league", Year = 15 }, new Calendar { Year = 15, Rounds = [] }, cancellationToken);
        return (group, name);
    }
}
