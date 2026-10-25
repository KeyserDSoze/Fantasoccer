using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

public sealed partial class McpApiTests
{
    [Fact]
    public async Task Generic_repository_queries_are_hidden_and_cached_identifiers_cannot_execute_them_even_as_global_admin()
    {
        var token = (await ConnectAsync("alessandro.rapiti44@gmail.com")).GetProperty("access_token").GetString()!;
        var catalog = _factory.Services.GetRequiredService<McpEndpointCatalog>();
        foreach (var path in new[] { "api/Team/Query", "api/Team/Query/Stream", "api/Rank/Operation", "api/AppIdentity/Query" })
        {
            var operation = catalog.Operations.Values.Single(x => x.Api.RelativePath == path);
            Assert.DoesNotContain(operation.Tool.Name, catalog.McpOperations.Keys);
            var listed = ToolData(await CallAsync(token, "list_api_operations", new { filter = path }));
            Assert.Empty(listed.GetProperty("items").EnumerateArray());
            var direct = await CallAsync(token, operation.Tool.Name, new { body = new { } });
            Assert.True(direct.GetProperty("isError").GetBoolean());
            Assert.Contains("point reads", direct.GetProperty("content")[0].GetProperty("text").GetString());
            var wrapped = await CallAsync(token, "execute_api_operation", new { operation = operation.Tool.Name, arguments = new { body = new { } } });
            Assert.True(wrapped.GetProperty("isError").GetBoolean());
            Assert.Contains("lookups", wrapped.GetProperty("content")[0].GetProperty("text").GetString());
        }
    }

    [Fact]
    public async Task Publish_all_tools_uses_the_filtered_catalog_too()
    {
        var token = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var options = _factory.Services.GetRequiredService<McpOptions>();
        var previous = options.PublishAllTools;
        try
        {
            options.PublishAllTools = true;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
            var response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode, text);
            if (text.StartsWith("event:") || text.StartsWith("data:")) text = text.Split('\n').First(x => x.StartsWith("data:"))[5..].Trim();
            var names = JsonSerializer.Deserialize<JsonElement>(text).GetProperty("result").GetProperty("tools").EnumerateArray()
                .Select(x => x.GetProperty("name").GetString()).ToHashSet();
            var catalog = _factory.Services.GetRequiredService<McpEndpointCatalog>();
            Assert.Contains(Find("api/Team/Get"), names);
            foreach (var hidden in catalog.Operations.Keys.Except(catalog.McpOperations.Keys)) Assert.DoesNotContain(hidden, names);
        }
        finally { options.PublishAllTools = previous; }
    }

    [Fact]
    public async Task Filtering_MCP_does_not_remove_authorization_from_legacy_HTTP_queries()
    {
        var email = "point-read-admin-" + Guid.NewGuid().ToString("N") + "@example.com";
        var frontend = await FrontendTokenAsync(email);
        var seed = await SeedCompositeKeyDataAsync(email, IdentityRole.Admin);
        var users = _factory.Services.GetRequiredService<IRepository<AppIdentity, string>>();
        var account = (await users.GetAsync(email, TestContext.Current.CancellationToken))!;
        account.Groups = [new() { Id = seed.Group, Name = "Admin group", Role = IdentityRole.Admin }];
        await users.UpdateAsync(email, account, TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", frontend);
        var allowed = await _client.PostAsJsonAsync("/api/AppIdentity/Query", new { }, TestContext.Current.CancellationToken);
        Assert.True(allowed.IsSuccessStatusCode, await allowed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var member = "point-read-member-" + Guid.NewGuid().ToString("N") + "@example.com";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await FrontendTokenAsync(member));
        foreach (var path in new[] { "/api/Group/Query", "/api/AppIdentity/Query" })
        {
            var forbidden = await _client.PostAsJsonAsync(path, new { }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
    }

    [Fact]
    public async Task Account_group_and_calendar_resolve_opponents_roster_and_actual_matchday_without_queries()
    {
        const string opponent = "point-read-opponent@example.com";
        var token = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var seed = await SeedCompositeKeyDataAsync(Email);
        var groups = _factory.Services.GetRequiredService<IRepository<Group, string>>();
        var group = (await groups.GetAsync(seed.Group, TestContext.Current.CancellationToken))!;
        group.Users.Add(new() { Email = opponent, Username = "OpponentUsername", Role = IdentityRole.Participant });
        group.Baskets[0].Years[0].Teams.Add(new() { Owner = opponent, Name = "Opponent team name", AdditionalOwners = [] });
        // Same name elsewhere must not override the basket referenced by the chosen league.
        group.Baskets.Add(new() { Id = "other-basket", Name = "Other basket", Years = [new() { Year = 15,
            Teams = [new() { Owner = opponent, Name = "Opponent team name", AdditionalOwners = [] }] }] });
        await groups.UpdateAsync(seed.Group, group, TestContext.Current.CancellationToken);
        var users = _factory.Services.GetRequiredService<IRepository<AppIdentity, string>>();
        var identity = (await users.GetAsync(Email, TestContext.Current.CancellationToken))!;
        identity.Groups = [new() { Id = seed.Group, Name = group.Name, Role = IdentityRole.Participant }];
        await users.UpdateAsync(Email, identity, TestContext.Current.CancellationToken);
        Team Roster(string playerName) => new()
        {
            Name = "Opponent team name", Owner = opponent, AdditionalOwners = [],
            Players = [new() { Name = playerName, Role = Role.Forward, Position = FantaSoccerRole.Forward,
                Price = 17, Status = PlayerInTeamStatus.Active, RealTeam = new() { Name = "Roma" } }]
        };
        await _factory.Services.GetRequiredService<IRepository<Team, TeamKey>>().InsertAsync(
            new() { Group = seed.Group, Year = 15, BasketId = "basket", Email = opponent }, Roster("Current roster player"), TestContext.Current.CancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Team, TeamDayKey>>().InsertAsync(
            new() { Group = seed.Group, Year = 15, BasketId = "basket", Email = opponent, Day = 9 }, Roster("Matchday nine player"), TestContext.Current.CancellationToken);
        await _factory.Services.GetRequiredService<IRepository<Calendar, LeagueKey>>().UpdateAsync(
            new() { Group = seed.Group, League = "league", Year = 15 }, new()
            {
                Year = 15, Rounds = new() { ["round"] = [new() { Number = 2, SerieADay = 9, Games =
                    [new() { Id = "point-read-game", Number = 2, Home = seed.TeamName, HomeOwner = Email, Away = "Opponent team name", AwayOwner = opponent }] }] }
            }, TestContext.Current.CancellationToken);

        async Task<JsonElement> Read(string path, string key)
        {
            var result = await CallAsync(token, "execute_api_operation", new { operation = Find(path), arguments = new { query = new { key } } });
            Assert.False(result.GetProperty("isError").GetBoolean(), result.GetRawText());
            var wrapper = ToolData(result);
            Assert.Equal(200, wrapper.GetProperty("statusCode").GetInt32());
            return JsonSerializer.Deserialize<JsonElement>(wrapper.GetProperty("body").GetString()!);
        }

        var accountData = ToolData(await CallAsync(token, "get_my_account", new { }));
        Assert.Equal("groups[].i", accountData.GetProperty("nextRead").GetProperty("keySource").GetString());
        var groupId = accountData.GetProperty("groups")[0].GetProperty("i").GetString()!;
        var groupData = await Read("api/Group/Get", groupId);
        var league = groupData.GetProperty("l")[0];
        var leagueId = league.GetProperty("i").GetString()!;
        var calendar = await Read("api/Calendar/Get", JsonSerializer.Serialize(new { g = groupId, l = leagueId, y = 15 }));
        var year = calendar.GetProperty("y").GetInt32();
        var day = calendar.GetProperty("r").GetProperty("round")[0];
        var game = day.GetProperty("g")[0];
        var owner = game.GetProperty("o").GetString() == accountData.GetProperty("email").GetString()
            ? game.GetProperty("u").GetString()! : game.GetProperty("o").GetString()!;
        Assert.Equal(opponent, owner); // Not Away/a or Group.Users[].Username/u.
        var allowedBaskets = league.GetProperty("b").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var basket = groupData.GetProperty("b").EnumerateArray().Single(x => allowedBaskets.Contains(x.GetProperty("i").GetString()) &&
            x.GetProperty("y").EnumerateArray().Any(y => y.GetProperty("y").GetInt32() == year &&
                y.GetProperty("t").EnumerateArray().Any(t => t.GetProperty("o").GetString() == owner)));
        var basketId = basket.GetProperty("i").GetString()!;
        var roster = await Read("api/Team/Get", JsonSerializer.Serialize(new { g = groupId, y = year, b = basketId, e = owner }));
        Assert.Equal("Current roster player", roster.GetProperty("p")[0].GetProperty("n").GetString());
        var matchday = day.GetProperty("a").GetInt32();
        Assert.Equal(9, matchday);
        Assert.NotEqual(day.GetProperty("n").GetInt32(), matchday);
        var lineup = await Read("api/Team/day/Get", JsonSerializer.Serialize(new { g = groupId, y = year, b = basketId, d = matchday, e = owner }));
        Assert.Equal("Matchday nine player", lineup.GetProperty("p")[0].GetProperty("n").GetString());
        var missingDay = await Read("api/Team/day/Exist", JsonSerializer.Serialize(new { g = groupId, y = year, b = basketId, d = 10, e = owner }));
        Assert.False(missingDay.GetProperty("i").GetBoolean());
        var fallback = await Read("api/Team/Get", JsonSerializer.Serialize(new { g = groupId, y = year, b = basketId, e = owner }));
        Assert.Equal("Current roster player", fallback.GetProperty("p")[0].GetProperty("n").GetString());
        var recipe = ToolData(await CallAsync(token, "list_api_operations", new { filter = "api/Team/Get" }));
        Assert.Equal("Fantasoccer.TeamKey", recipe.GetProperty("items")[0].GetProperty("pointReadRecipe").GetProperty("keyModel").GetString());
        var guide = ToolData(await CallAsync(token, "get_fantasoccer_guide", new { topic = "lookups" }));
        Assert.True(guide.GetProperty("pointReadRecipes").GetArrayLength() >= 6);
    }

    private static JsonElement ToolData(JsonElement result)
        => JsonSerializer.Deserialize<JsonElement>(result.GetProperty("content")[0].GetProperty("text").GetString()!);
}

[Collection("API integration")]
public sealed class McpPointReadCatalogTests(ApiFactory factory)
{
    [Theory]
    [InlineData("api/Team/Query", false)]
    [InlineData("api/Team/Query/Stream", false)]
    [InlineData("api/Team/day/Query/Stream", false)]
    [InlineData("api/Rank/Count", false)]
    [InlineData("api/Rank/Operation", false)]
    [InlineData("api/Rank/Operation/Stream", false)]
    [InlineData("api/team/query/stream", false)]
    [InlineData("api/Team/Get", true)]
    [InlineData("api/Team/day/Exist", true)]
    [InlineData("api/Team/Update", true)]
    [InlineData("Game/SaveTeam", true)]
    public void Only_generic_repository_scans_and_aggregations_are_removed(string path, bool expected)
    {
        var api = new ApiDescription { RelativePath = path, HttpMethod = "POST", ActionDescriptor = new ActionDescriptor() };
        Assert.Equal(expected, McpEndpointCatalog.IsExposedToMcp(new(api, new Tool { Name = "test" }, false, null)));
        api.ActionDescriptor = new ControllerActionDescriptor();
        Assert.True(McpEndpointCatalog.IsExposedToMcp(new(api, new Tool { Name = "controller-test" }, false, null)));
    }

    [Fact]
    public void Point_read_recipes_use_existing_exposed_endpoints_and_complete_key_shapes()
    {
        var catalog = factory.Services.GetRequiredService<McpEndpointCatalog>();
        foreach (var recipe in McpPointReadRecipes.All)
        {
            var operation = Assert.Single(catalog.McpOperations.Values, x => x.Api.RelativePath == recipe.Path);
            Assert.True(operation.ReadOnly);
            Assert.NotNull(operation.Repository);
            Assert.Equal(McpJsonContracts.ModelId(operation.Repository.KeyType), recipe.KeyModel);
            var key = recipe.ExampleArguments.GetProperty("query").GetProperty("key").GetString()!;
            if (typeof(IDefaultKey).IsAssignableFrom(operation.Repository.KeyType))
            {
                var converted = McpRepositoryKeyConverter.Convert(operation.Repository.KeyType, key);
                Assert.Equal(JsonSerializer.Deserialize<JsonElement>(converted.CanonicalJson).EnumerateObject().Count(), recipe.KeySources.Count);
            }
        }
        Assert.NotEmpty(catalog.Operations.Keys.Except(catalog.McpOperations.Keys));
        Assert.DoesNotContain(catalog.McpOperations.Values, x => !McpEndpointCatalog.IsExposedToMcp(x));
    }
}
