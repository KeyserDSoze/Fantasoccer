using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Fantasoccer.Application.Api.Mcp.Storage;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class McpApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private const string Email = "mcp-owner@example.com";
    private const string Resource = "https://localhost/mcp";
    private const string Redirect = "https://chatgpt.com/test-mcp-callback";

    public McpApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Metadata_and_unauthenticated_challenge_are_discoverable()
    {
        var metadata = await _client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp", TestContext.Current.CancellationToken);
        Assert.Equal(Resource, metadata.GetProperty("resource").GetString());
        var discovery = await _client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", TestContext.Current.CancellationToken);
        Assert.Contains("S256", discovery.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(metadata.GetProperty("authorization_servers")[0].GetString(), discovery.GetProperty("issuer").GetString());
        Assert.DoesNotContain("openid", discovery.GetProperty("scopes_supported").EnumerateArray().Select(x => x.GetString()));
        var response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public void Catalog_contains_controllers_repositories_uploads_but_never_authentication()
    {
        var operations = _factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values;
        Assert.True(operations.Any(x => x.Api.RelativePath == "api/Group/Get"), string.Join("\n", operations.Select(x => x.Api.RelativePath)));
        Assert.Contains(operations, x => x.Api.RelativePath == "Market/Create");
        Assert.Contains(operations, x => x.Api.RelativePath!.Contains("UploadLogo"));
        Assert.DoesNotContain(operations, x => x.Api.RelativePath!.Contains("Authentication") || x.Api.RelativePath.StartsWith("connect/"));
        Assert.DoesNotContain(operations, x => x.Api.RelativePath!.Contains("OAuthDocument"));
        Assert.False(operations.Single(x => x.Api.RelativePath == "LeagueManager/CreateCalendarAndRank").ReadOnly);
        Assert.False(operations.Single(x => x.Api.RelativePath == "api/Group/Delete").ReadOnly);
    }

    [Fact]
    public async Task OAuth_uses_existing_bearer_format_and_executes_real_controller_and_repository()
    {
        var tokens = await ConnectAsync();
        var access = tokens.GetProperty("access_token").GetString()!;
        var ticket = _factory.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(BearerTokenDefaults.AuthenticationScheme).BearerTokenProtector.Unprotect(access);
        Assert.NotNull(ticket);
        Assert.Equal(Email, ticket.Principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(Resource, ticket.Principal.FindFirstValue(McpOptions.ResourceClaim));

        var settings = Find("Settings/Get");
        var result = await CallAsync(access, settings, new { });
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Contains("200", result.GetProperty("content")[0].GetProperty("text").GetString());

        var identity = Find("api/AppIdentity/Get");
        result = await CallAsync(access, identity, new { query = new { key = Email } });
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Contains(Email, result.GetProperty("content")[0].GetProperty("text").GetString());

        result = await CallAsync(access, identity, new { query = new { key = "someone-else@example.com" } });
        Assert.True(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Frontend_token_is_not_an_OAuth_MCP_token_but_still_works_for_existing_API()
    {
        var token = await FrontendTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/api/Authentication/Social/User", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Contains(Email, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_works_and_disconnect_revokes_existing_access_and_refresh()
    {
        var tokens = await ConnectAsync();
        var access = tokens.GetProperty("access_token").GetString()!;
        var refresh = tokens.GetProperty("refresh_token").GetString()!;
        var refreshed = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["grant_type"] = "refresh_token", ["refresh_token"] = refresh, ["resource"] = Resource
        }), TestContext.Current.CancellationToken);
        Assert.True(refreshed.IsSuccessStatusCode, await refreshed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var fresh = await refreshed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await FrontendTokenAsync());
        var disconnected = await _client.PostAsJsonAsync("/connect/disconnect", new { clientId = "chatgpt-test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, disconnected.StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
        response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["grant_type"] = "refresh_token",
            ["refresh_token"] = fresh.GetProperty("refresh_token").GetString()!, ["resource"] = Resource
        }), TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest ||
            response.Headers.Location?.Query.Contains("error=") == true);
    }

    [Fact]
    public async Task Invalid_redirect_and_missing_PKCE_never_reach_login()
    {
        var url = AuthorizationUrl(new string('x', 43));
        var response = await _client.GetAsync(url.Replace(Uri.EscapeDataString(Redirect), Uri.EscapeDataString("https://evil.example/callback")), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await _client.GetAsync(url.Replace("code_challenge_method=S256", "code_challenge_method=plain"), TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest ||
            response.Headers.Location?.Query.Contains("error=") == true);
    }

    [Fact]
    public async Task Consent_requires_frontend_bearer_origin_and_browser_binding()
    {
        var response = await _client.GetAsync(AuthorizationUrl(new string('x', 43)), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["mcp_request"].ToString();
        response = await _client.PostAsJsonAsync("/connect/approve", new { transactionId = id, approved = true }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await FrontendTokenAsync());
        response = await _client.PostAsJsonAsync("/connect/approve", new { transactionId = id, approved = true }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _client.DefaultRequestHeaders.Add("Origin", "https://frontend.example");
        response = await _client.PostAsJsonAsync("/connect/approve", new { transactionId = id, approved = true }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var approved = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var otherBrowser = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        response = await otherBrowser.GetAsync(approved.GetProperty("redirectUrl").GetString(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_tools_can_discover_and_execute_without_publishing_hundreds_of_tools()
    {
        var tokens = await ConnectAsync();
        var token = tokens.GetProperty("access_token").GetString()!;
        var account = await CallAsync(token, "get_my_account", new { });
        Assert.Contains(Email, account.GetProperty("content")[0].GetProperty("text").GetString());
        var catalog = await CallAsync(token, "list_api_operations", new { filter = "Settings/Get" });
        Assert.Contains(Find("Settings/Get"), catalog.GetProperty("content")[0].GetProperty("text").GetString());
        var result = await CallAsync(token, "execute_api_operation", new { operation = Find("Settings/Get"), arguments = new { } });
        Assert.False(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Writes_execute_controller_logic_and_do_not_allow_admin_actions_or_identity_escalation()
    {
        var token = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var updated = await CallAsync(token, Find("Settings/Update"), new { body = new { AutoOptimalFormation = true, LiveWidgetEnabled = false } });
        Assert.False(updated.GetProperty("isError").GetBoolean());
        var read = await CallAsync(token, Find("Settings/Get"), new { });
        Assert.Contains("200", read.GetProperty("content")[0].GetProperty("text").GetString());
        var denied = await CallAsync(token, Find("Card/CreateCard"), new { body = new { Name = "Not an admin" } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        denied = await CallAsync(token, Find("api/AppIdentity/Update"), new
        {
            query = new { key = Email }, body = new { e = Email, u = "MCP owner", s = new { }, g = new[] { new { i = "forged", n = "Forged group", r = 8 } } }
        });
        Assert.True(denied.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Legacy_repository_URL_cannot_modify_the_authorization_anchors_without_permission()
    {
        await FrontendTokenAsync();
        _client.DefaultRequestHeaders.Add("X-Test-User", Email);
        var response = await _client.PostAsJsonAsync("/api/AppIdentity/Update?key=" + Email,
            new { e = Email, u = "MCP owner", s = new { }, g = new[] { new { i = "forged", n = "Forged group", r = 8 } } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await _client.PostAsJsonAsync("/api/Group/Insert?key=forged", new { i = "forged" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Group_membership_is_checked_for_reads_writes_and_ambiguous_composite_keys()
    {
        var token = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var id = "mcp-group-" + Guid.NewGuid().ToString("N");
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRepository<Group, string>>().InsertAsync(id,
            new Group { Id = id, Name = "MCP group", Users = [new UserOfAGroup { Email = Email, Role = IdentityRole.Reader }], Leagues = [], Baskets = [] }, TestContext.Current.CancellationToken);
        var read = await CallAsync(token, Find("api/Group/Get"), new { query = new { key = id } });
        Assert.False(read.GetProperty("isError").GetBoolean());
        var denied = await CallAsync(token, Find("api/Group/Get"), new { query = new { key = "other-group" } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        denied = await CallAsync(token, Find("LeagueManager/CreateCalendarAndRank"), new { query = new { groupId = id, leagueId = "none", year = 2026 } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        denied = await CallAsync(token, Find("api/Team/Get"), new { query = new { key = "{\"g\":\"" + id + "\",\"Group\":\"other-group\",\"y\":2026,\"e\":\"victim@example.com\",\"b\":\"basket\"}" } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        var groups = scope.ServiceProvider.GetRequiredService<IRepository<Group, string>>();
        var group = await groups.GetAsync(id, TestContext.Current.CancellationToken);
        group!.Users.Clear();
        await groups.UpdateAsync(id, group, TestContext.Current.CancellationToken);
        denied = await CallAsync(token, Find("api/Group/Get"), new { query = new { key = id } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Only_actual_global_admin_can_use_admin_endpoints_and_multipart_model_binding_is_preserved()
    {
        var token = (await ConnectAsync("alessandro.rapiti44@gmail.com")).GetProperty("access_token").GetString()!;
        var result = await CallAsync(token, Find("Card/CreateCard"), new { body = new { Name = "" } });
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains("400", text); // Controller validation, not an authorization bypass or a 403.
        var upload = Find("Card/UploadCardImage/image/{cardId}");
        result = await CallAsync(token, upload, new
        {
            route = new { cardId = "missing-mcp-card" },
            files = new { file = new { fileName = "test.png", contentType = "image/png", base64 = "aGVsbG8=" } }
        });
        Assert.Contains("404", result.GetProperty("content")[0].GetProperty("text").GetString());
        var forgedAdmin = (await ConnectAsync("mcp-display-name-admin@example.com", "alessandro.rapiti44@gmail.com")).GetProperty("access_token").GetString()!;
        result = await CallAsync(forgedAdmin, Find("Card/CreateCard"), new { body = new { Name = "" } });
        Assert.Contains("Forbidden", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task PKCE_is_verified_and_authorization_codes_cannot_be_replayed()
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var code = await AuthorizeCodeAsync(verifier);
        var invalid = await ExchangeAsync(code, new string('z', 43));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var valid = await ExchangeAsync(code, verifier);
        Assert.True(valid.IsSuccessStatusCode, await valid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var replay = await ExchangeAsync(code, verifier);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task OAuth_access_token_revocation_is_effective_and_cannot_be_used_as_refresh_token()
    {
        var access = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["grant_type"] = "refresh_token", ["refresh_token"] = access, ["resource"] = Resource
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await _client.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["token"] = access, ["token_type_hint"] = "access_token"
        }), TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auction_commands_are_discoverable_without_access_to_other_auctions()
    {
        var token = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var commands = await CallAsync(token, Find("McpAuction/Commands"), new { });
        Assert.Contains("PlaceBid", commands.GetProperty("content")[0].GetProperty("text").GetString());
        var result = await CallAsync(token, Find("McpAuction/Invoke"), new { body = new { operation = "PlaceBid", arguments = new { auctionId = "unknown-mcp-auction", amount = 10 } } });
        Assert.Contains("404", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Group_admin_can_provision_members_but_cannot_grant_super_admin_or_use_global_admin_actions()
    {
        var admin = "mcp-group-admin@example.com";
        var id = "mcp-admin-group-" + Guid.NewGuid().ToString("N");
        await FrontendTokenAsync(admin);
        using var scope = _factory.Services.CreateScope();
        var identities = scope.ServiceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
        var identity = await identities.GetAsync(admin, TestContext.Current.CancellationToken);
        identity!.Groups = [new GroupForIdentity { Id = id, Name = "Admin group", Role = IdentityRole.Admin }];
        await identities.UpdateAsync(admin, identity, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<IRepository<Group, string>>().InsertAsync(id, new Group
        {
            Id = id, Name = "Admin group", Users = [new UserOfAGroup { Email = admin, Role = IdentityRole.Admin }], Leagues = [], Baskets = []
        }, TestContext.Current.CancellationToken);
        var token = (await ConnectAsync(admin)).GetProperty("access_token").GetString()!;
        var invitee = "mcp-invitee-" + Guid.NewGuid().ToString("N") + "@example.com";
        var inserted = await CallAsync(token, Find("api/AppIdentity/Insert"), new
        {
            query = new { key = invitee }, body = new { e = invitee, u = invitee, s = new { }, g = new[] { new { i = id, n = "Admin group", r = 1 } } }
        });
        Assert.False(inserted.GetProperty("isError").GetBoolean());
        var read = await CallAsync(token, Find("api/AppIdentity/Get"), new { query = new { key = invitee } });
        Assert.False(read.GetProperty("isError").GetBoolean());
        var denied = await CallAsync(token, Find("api/AppIdentity/Update"), new
        {
            query = new { key = invitee }, body = new { e = invitee, u = invitee, s = new { }, g = new[] { new { i = id, n = "Admin group", r = 8 } } }
        });
        Assert.True(denied.GetProperty("isError").GetBoolean());
        denied = await CallAsync(token, Find("Card/CreateCard"), new { body = new { Name = "Not global admin" } });
        Assert.True(denied.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Expired_and_wrong_audience_tickets_are_rejected()
    {
        var access = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var protector = _factory.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(BearerTokenDefaults.AuthenticationScheme).BearerTokenProtector;
        var ticket = protector.Unprotect(access)!;
        ticket.Principal.SetAudiences("https://another.example/mcp");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", protector.Protect(ticket));
        var response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ticket = protector.Unprotect(access)!;
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", protector.Protect(ticket));
        response = await _client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_requests_keep_their_authenticated_identity_isolated()
    {
        const string other = "mcp-concurrent-other@example.com";
        var first = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        var second = (await ConnectAsync(other)).GetProperty("access_token").GetString()!;
        var operation = Find("api/AppIdentity/Get");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            var email = index % 2 == 0 ? Email : other;
            var token = index % 2 == 0 ? first : second;
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var result = await CallAsync(token, operation, new { query = new { key = email } }, client);
            Assert.False(result.GetProperty("isError").GetBoolean());
            Assert.Contains(email, result.GetProperty("content")[0].GetProperty("text").GetString());
        }));
    }

    private string Find(string path) => _factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values.Single(x => x.Api.RelativePath == path).Tool.Name;

    [Fact]
    public async Task Owner_cannot_use_MCP_repository_tools_to_remove_sell_replace_players_or_change_prices()
    {
        var access = (await ConnectAsync()).GetProperty("access_token").GetString()!;
        using var scope = _factory.Services.CreateScope();
        var id = "mcp-roster-" + Guid.NewGuid().ToString("N");
        await scope.ServiceProvider.GetRequiredService<IRepository<Group, string>>().InsertAsync(id, new Group
        {
            Id = id, Name = "Roster group", Users = [new() { Email = Email, Role = IdentityRole.Participant }], Leagues = [],
            Baskets = [new() { Id = "basket", Name = "Basket", Years = [new() { Year = 2098, Teams = [new() { Owner = Email, Name = "Team", AdditionalOwners = [] }] }] }]
        }, TestContext.Current.CancellationToken);
        var key = new TeamKey { Group = id, Year = 2098, BasketId = "basket", Email = Email };
        var roster = new Team { Owner = Email, Name = "Team", AdditionalOwners = [], MoneyFromRank = 42,
            Players = [new() { Name = "Trusted player", Price = 10, Status = PlayerInTeamStatus.Active, RealTeam = new() { Name = "Roma" } }] };
        var teams = scope.ServiceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
        await teams.InsertAsync(key, roster, TestContext.Current.CancellationToken);
        var before = JsonSerializer.Serialize(roster);
        foreach (var mutation in new[] { "remove", "sell", "replace", "price", "balance" })
        {
            var forged = roster.Clone();
            switch (mutation)
            {
                case "remove": forged.Players.Clear(); break;
                case "sell": forged.Players[0].Status = PlayerInTeamStatus.Sold; forged.Players[0].Revenue = 99999; break;
                case "replace": forged.Players[0].Name = "Other team's player"; break;
                case "price": forged.Players[0].Price = 0; break;
                case "balance": forged.MoneyFromRank = 99999; break;
            }
            var result = await CallAsync(access, Find("api/Team/Update"), new { query = new { key = JsonSerializer.Serialize(key) }, body = forged });
            Assert.True(result.GetProperty("isError").GetBoolean());
        }
        var deleted = await CallAsync(access, Find("api/Team/Delete"), new { query = new { key = JsonSerializer.Serialize(key) } });
        Assert.True(deleted.GetProperty("isError").GetBoolean());
        Assert.Equal(before, JsonSerializer.Serialize(await teams.GetAsync(key, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Concurrent_code_redemptions_have_exactly_one_winner()
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var code = await AuthorizeCodeAsync(verifier);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ExchangeAsync(code, verifier)));
        Assert.Single(responses.Where(x => x.IsSuccessStatusCode));
        Assert.All(responses.Where(x => !x.IsSuccessStatusCode), x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
    }

    [Fact]
    public async Task Concurrent_refresh_exchanges_cannot_rotate_the_same_token_twice()
    {
        var token = (await ConnectAsync("mcp-refresh-race-" + Guid.NewGuid().ToString("N") + "@example.com"))
            .GetProperty("refresh_token").GetString()!;
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "chatgpt-test", ["grant_type"] = "refresh_token", ["refresh_token"] = token, ["resource"] = Resource
            }), TestContext.Current.CancellationToken)));
        Assert.Single(responses.Where(x => x.IsSuccessStatusCode));
        Assert.All(responses.Where(x => !x.IsSuccessStatusCode), x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
    }

    [Fact]
    public async Task Consent_survives_new_service_instances_and_transitions_only_once()
    {
        var storage = _factory.Services.GetRequiredService<IOAuthDocumentStorage>();
        var first = new McpConsentTransactions(storage);
        var id = await first.CreateAsync("/connect/authorize?client_id=chatgpt-test", "ChatGPT", TestContext.Current.CancellationToken);
        var restarted = new McpConsentTransactions(storage);
        Assert.NotNull(await restarted.GetAsync(id, TestContext.Current.CancellationToken));
        var approvals = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            new McpConsentTransactions(storage).ApproveAsync(id, Email, TestContext.Current.CancellationToken)));
        Assert.Single(approvals.Where(x => x != null));
        var completions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            new McpConsentTransactions(storage).ConsumeAsync(id, TestContext.Current.CancellationToken)));
        Assert.Single(completions.Where(x => x != null));
        var redemption = await restarted.RedeemAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(Email, redemption!.Email);
        Assert.Null(await first.RedeemAsync(id, TestContext.Current.CancellationToken));
        Assert.Null(await first.GetAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Independent_store_instances_detect_stale_updates_and_keep_retention_safe()
    {
        var storage = _factory.Services.GetRequiredService<IOAuthDocumentStorage>();
        var first = new OAuthStore<OAuthToken>(storage);
        var second = new OAuthStore<OAuthToken>(storage);
        var token = new OAuthToken { Status = OpenIddictConstants.Statuses.Valid, Subject = Email,
            CreationDate = DateTimeOffset.UtcNow, ExpirationDate = DateTimeOffset.UtcNow.AddHours(1),
            Properties = System.Collections.Immutable.ImmutableDictionary<string, JsonElement>.Empty.Add("test", JsonSerializer.SerializeToElement(new { value = 42 })) };
        await first.CreateAsync(token, TestContext.Current.CancellationToken);
        var stale = await second.FindByIdAsync(token.Id, TestContext.Current.CancellationToken);
        token.Status = OpenIddictConstants.Statuses.Redeemed;
        await first.UpdateAsync(token, TestContext.Current.CancellationToken);
        stale!.Status = OpenIddictConstants.Statuses.Valid;
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(() => second.UpdateAsync(stale, TestContext.Current.CancellationToken).AsTask());
        var current = await second.FindByIdAsync(token.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, current!.Status);
        Assert.Equal(42, current.Properties["test"].GetProperty("value").GetInt32());
        // Retention can remove an expired record without touching a live token/grant.
        var id = "retention-" + Guid.NewGuid().ToString("N");
        Assert.True(await storage.WriteAsync(new OAuthDocument { Id = id, Kind = "retention", Version = "1", Payload = "{}",
            DeleteAfterUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }, null, TestContext.Current.CancellationToken));
        await storage.PruneAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Null(await storage.GetAsync(id, TestContext.Current.CancellationToken));
        Assert.NotNull(await first.FindByIdAsync(token.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Connection_management_is_scoped_to_frontend_account_and_never_returns_credentials()
    {
        var email = "mcp-connections-" + Guid.NewGuid().ToString("N") + "@example.com";
        var access = (await ConnectAsync(email)).GetProperty("access_token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await FrontendTokenAsync(email));
        var response = await _client.GetAsync("/connect/connections", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var connections = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Single(connections.EnumerateArray());
        Assert.Equal("chatgpt-test", connections[0].GetProperty("clientId").GetString());
        Assert.DoesNotContain("token", connections.GetRawText(), StringComparison.OrdinalIgnoreCase);
        response = await _client.PostAsJsonAsync("/connect/disconnect", new { clientId = "chatgpt-test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        connections = await _client.GetFromJsonAsync<JsonElement>("/connect/connections", TestContext.Current.CancellationToken);
        Assert.Empty(connections.EnumerateArray());
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        response = await _client.GetAsync("/connect/connections", TestContext.Current.CancellationToken);
        Assert.False(response.IsSuccessStatusCode);
    }

    private async Task<JsonElement> CallAsync(string token, string name, object arguments, HttpClient? client = null)
    {
        client ??= _client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        client.DefaultRequestHeaders.Remove("MCP-Protocol-Version");
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-11-25");
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name, arguments } }, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, text);
        if (text.StartsWith("event:") || text.StartsWith("data:")) text = text.Split('\n').First(x => x.StartsWith("data:"))[5..].Trim();
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(json.TryGetProperty("error", out _), text);
        return json.GetProperty("result").Clone();
    }

    private async Task<string> FrontendTokenAsync(string email = Email, string username = "MCP owner")
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
        if (await users.GetAsync(email, TestContext.Current.CancellationToken) == null)
            await users.InsertAsync(email, new AppIdentity { Email = email, Username = username, Groups = [] }, TestContext.Current.CancellationToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, email)], "Bearer"));
        return scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme)
            .BearerTokenProtector.Protect(new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) }, "Bearer"));
    }

    private static string AuthorizationUrl(string verifier) => QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
    {
        ["client_id"] = "chatgpt-test", ["redirect_uri"] = Redirect, ["response_type"] = "code",
        ["scope"] = McpOptions.Scope + " offline_access", ["resource"] = Resource, ["state"] = "test-state",
        ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256"
    });

    private async Task<JsonElement> ConnectAsync(string email = Email, string username = "MCP owner")
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var code = await AuthorizeCodeAsync(verifier, email, username);
        var response = await ExchangeAsync(code, verifier);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<string> AuthorizeCodeAsync(string verifier, string email = Email, string username = "MCP owner")
    {
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("Origin");
        var response = await _client.GetAsync(AuthorizationUrl(verifier), TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["mcp_request"].ToString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await FrontendTokenAsync(email, username));
        _client.DefaultRequestHeaders.Add("Origin", "https://frontend.example");
        response = await _client.PostAsJsonAsync("/connect/approve", new { transactionId = id, approved = true }, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var approval = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("Origin");
        response = await _client.GetAsync(approval.GetProperty("redirectUrl").GetString(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        response = await _client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith(Redirect, response.Headers.Location!.AbsoluteUri);
        var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
        Assert.False(query.ContainsKey("error"), response.Headers.Location.AbsoluteUri);
        Assert.Equal("test-state", query["state"].ToString());
        return query["code"].ToString();
    }

    private Task<HttpResponseMessage> ExchangeAsync(string code, string verifier)
        => _client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["grant_type"] = "authorization_code", ["code"] = code,
            ["redirect_uri"] = Redirect, ["code_verifier"] = verifier, ["resource"] = Resource
        }), TestContext.Current.CancellationToken);

    public void Dispose()
    {
        _client.Dispose();
    }
}
