using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Auctions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task SQL_auction_HTTP_and_SignalR_share_authorization_versions_budgets_assignments_and_presence()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var participant = await factory.AccountAsync();
        using var coOwner = await factory.AccountAsync();
        using var reader = await factory.AccountAsync();
        using var outsider = await factory.AccountAsync();
        var year = FantacalcioTime.ActualYear;
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, year);
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        var accounts = new List<AccountIdentityDto>();
        foreach (var client in new[] { manager, participant, coOwner, reader }) accounts.Add((await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!);
        for (var i = 1; i < accounts.Count; i++)
            (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(accounts[i].Email, "Auction member", i == 3 ? 1 : 2))).EnsureSuccessStatusCode();
        var teams = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var result = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Auction team " + i, accounts[i].Id, i == 1 ? [accounts[2].Id] : []));
            result.EnsureSuccessStatusCode(); teams.Add((await result.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId);
        }
        var players = new List<string>();
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).IsMain = true;
            var club = new FootballClub { Name = "Auction runtime club", Abbreviation = "arc" };
            database.Clubs.Add(club);
            for (var i = 0; i < 2; i++)
            {
                var person = new FootballPlayer { Name = "Auction runtime " + Guid.NewGuid().ToString("N") };
                database.Players.Add(person);
                database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(person.Name), Position = PlayerPosition.Forward, IsActive = true });
                await database.SaveChangesAsync(); players.Add(person.Id);
            }
        }
        var create = await manager.PostAsJsonAsync("/api/auctions", new CreateAuctionRequest(group.GroupId, league, year, 2, 0));
        create.EnsureSuccessStatusCode();
        var id = await create.Content.ReadFromJsonAsync<Guid>();
        var path = $"/api/auctions/{id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        var state = (await manager.GetFromJsonAsync<AuctionStateDto>(path))!;
        Assert.True(state.IsAuctioneer);
        Assert.False((await reader.GetFromJsonAsync<AuctionStateDto>(path))!.IsAuctioneer);
        var json = await manager.GetFromJsonAsync<JsonElement>(path);
        Assert.False(json.GetProperty("wrapper").TryGetProperty("playerOrder", out _));
        Assert.False(json.GetProperty("wrapper").TryGetProperty("listOfPlayers", out _));
        await using var connection = await RawAuctionConnection.ConnectAsync(reader);
        var joined = await connection.InvokeAsync("JoinAsync", id);
        Assert.False(joined.TryGetProperty("error", out _));
        Assert.Contains(accounts[3].Email, (await manager.GetFromJsonAsync<string[]>(path + "/presence"))!);
        var forbiddenHub = await connection.InvokeAsync("CommandAsync", id, "show", new AuctionCommandRequest(state.Wrapper.Version, Role: 3, PlayerId: players[0]));
        Assert.True(forbiddenHub.TryGetProperty("error", out _));
        var shown = await manager.PostAsJsonAsync(path + "/commands/show", new AuctionCommandRequest(state.Wrapper.Version, Role: 3, PlayerId: players[0]));
        shown.EnsureSuccessStatusCode();
        var update = (await shown.Content.ReadFromJsonAsync<AuctionUpdateDto>())!;
        await using var bidderConnection = await RawAuctionConnection.ConnectAsync(participant);
        await bidderConnection.InvokeAsync("JoinAsync", id);
        var firstBid = await bidderConnection.InvokeAsync("CommandAsync", id, "bid", new AuctionCommandRequest(update.Version, TeamId: teams[1], Amount: 20));
        Assert.False(firstBid.TryGetProperty("error", out _));
        var bidding = firstBid.GetProperty("result").Deserialize<AuctionUpdateDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync(path + "/commands/bid", new AuctionCommandRequest(bidding.Version, TeamId: teams[0], Amount: 30))).StatusCode);
        var managerBid = await manager.PostAsJsonAsync(path + "/commands/bid", new AuctionCommandRequest(bidding.Version, TeamId: teams[0], Amount: 30));
        managerBid.EnsureSuccessStatusCode();
        var version = (await managerBid.Content.ReadFromJsonAsync<AuctionUpdateDto>())!.Version;
        var bids = await Task.WhenAll(participant.PostAsJsonAsync(path + "/commands/bid", new AuctionCommandRequest(version, TeamId: teams[1], Amount: 40)),
            coOwner.PostAsJsonAsync(path + "/commands/bid", new AuctionCommandRequest(version, TeamId: teams[1], Amount: 50)));
        Assert.Single(bids.Where(x => x.IsSuccessStatusCode));
        Assert.Single(bids.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        state = (await manager.GetFromJsonAsync<AuctionStateDto>(path))!;
        Assert.Equal(teams[1], state.Wrapper.Actual!.WinningTeamId);
        var price = state.Wrapper.Actual.Price;
        var assigned = await manager.PostAsJsonAsync(path + "/commands/assign", new AuctionCommandRequest(state.Wrapper.Version));
        assigned.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(path + "/commands/assign", new AuctionCommandRequest(state.Wrapper.Version))).StatusCode);
        state = (await manager.GetFromJsonAsync<AuctionStateDto>(path))!;
        var roster = state.Teams.Single(x => x.Value.TeamId == teams[1]).Value;
        Assert.Equal(price, Assert.Single(roster.Players).Price);
        Assert.Equal(players[0], roster.Players[0].PlayerId);
        Assert.Equal(1, state.Wrapper.Winnings.Length);
        Assert.Null(state.Wrapper.Actual);
        var status = await manager.PostAsJsonAsync(path + "/commands/player-status", new AuctionCommandRequest(state.Wrapper.Version, PlayerId: players[0], TeamId: teams[1], PlayerStatus: 3,
            Revenue: 25, RosterOrdinal: roster.Players[0].Ordinal, ExpectedRosterVersion: roster.Version));
        status.EnsureSuccessStatusCode();
        state = (await manager.GetFromJsonAsync<AuctionStateDto>(path))!;
        roster = state.Teams.Single(x => x.Value.TeamId == teams[1]).Value;
        Assert.Equal(25, roster.Players[0].Revenue);
        (await manager.PostAsJsonAsync(path + "/commands/player-status", new AuctionCommandRequest(state.Wrapper.Version, PlayerId: players[0], TeamId: teams[1], PlayerStatus: 4,
            RosterOrdinal: roster.Players[0].Ordinal, ExpectedRosterVersion: roster.Version))).EnsureSuccessStatusCode();
        await using var final = factory.Database.CreateContext();
        Assert.Equal(2, await final.AuctionRosterChanges.CountAsync(x => x.AuctionId == id));
        Assert.Equal(1, (await final.AuctionSessions.SingleAsync(x => x.Id == id)).Winnings.Count);
    }

    [PostgreSqlFact]
    public async Task Opaque_bearer_query_tokens_are_hub_only_and_MCP_resource_tokens_cannot_negotiate_the_hub()
    {
        using var accountClient = await factory.AccountAsync();
        var account = (await accountClient.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        using var nativeAuth = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = BearerTokenDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = BearerTokenDefaults.AuthenticationScheme;
        })));
        using var client = nativeAuth.CreateClient();
        var options = nativeAuth.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme);
        string Token(bool mcp)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, account.Email), new(ClaimTypes.Email, account.Email), new(ClaimTypes.NameIdentifier, account.Id.ToString()) };
            if (mcp) claims.Add(new("fantasoccer:mcp:resource", "test-resource"));
            return options.BearerTokenProtector.Protect(new(new(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, BearerTokenDefaults.AuthenticationScheme));
        }
        var bearer = Uri.EscapeDataString(Token(false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/accounts/me?access_token=" + bearer)).StatusCode);
        (await client.PostAsync("/auctionhub/negotiate?negotiateVersion=1&access_token=" + bearer, null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/auctionhub/negotiate?negotiateVersion=1&access_token=" + Uri.EscapeDataString(Token(true)), null)).StatusCode);
    }
}

internal sealed class RawAuctionConnection(HttpClient client, string token) : IAsyncDisposable
{
    private readonly string _path = "/auctionhub?id=" + Uri.EscapeDataString(token);
    private int _sequence;
    public static async Task<RawAuctionConnection> ConnectAsync(HttpClient client)
    {
        var response = await client.PostAsync("/auctionhub/negotiate?negotiateVersion=1", null);
        response.EnsureSuccessStatusCode();
        var negotiation = await response.Content.ReadFromJsonAsync<JsonElement>();
        var connection = new RawAuctionConnection(client, negotiation.GetProperty("connectionToken").GetString()!);
        (await client.GetAsync(connection._path)).EnsureSuccessStatusCode();
        (await client.PostAsync(connection._path, new StringContent("{\"protocol\":\"json\",\"version\":1}\u001e", Encoding.UTF8, "text/plain"))).EnsureSuccessStatusCode();
        return connection;
    }
    public async Task<JsonElement> InvokeAsync(string target, params object[] arguments)
    {
        var invocation = (++_sequence).ToString();
        var json = JsonSerializer.Serialize(new { type = 1, invocationId = invocation, target, arguments }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + '\u001e';
        (await client.PostAsync(_path, new StringContent(json, Encoding.UTF8, "text/plain"))).EnsureSuccessStatusCode();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            var response = await client.GetAsync(_path, timeout.Token);
            response.EnsureSuccessStatusCode();
            foreach (var payload in (await response.Content.ReadAsStringAsync(timeout.Token)).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                var message = JsonSerializer.Deserialize<JsonElement>(payload);
                if (message.TryGetProperty("type", out var type) && type.GetInt32() == 3 && message.GetProperty("invocationId").GetString() == invocation) return message;
            }
        }
        throw new TimeoutException("SignalR invocation did not complete.");
    }
    public async ValueTask DisposeAsync() { await client.DeleteAsync(_path); }
}
