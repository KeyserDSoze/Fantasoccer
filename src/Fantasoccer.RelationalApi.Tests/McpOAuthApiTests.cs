using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

// Exercises the real OAuth/MCP transport on PostgreSQL, not the retired repository API.
public sealed class McpOAuthApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private const string Resource = "https://localhost/mcp";
    private const string Redirect = "https://chatgpt.com/test-mcp-callback";

    private WebApplicationFactory<Program> App() => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Mcp:Enabled", "true");
        builder.UseSetting("Mcp:Issuer", "https://localhost");
        builder.UseSetting("Mcp:FrontendUrl", "https://frontend.example/");
        builder.UseSetting("Mcp:Clients:0:ClientId", "chatgpt-test");
        builder.UseSetting("Mcp:Clients:0:RedirectUris:0", Redirect);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, ClientAddressFilter>();
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = BearerTokenDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = BearerTokenDefaults.AuthenticationScheme;
            });
        });
    });

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new()
    {
        BaseAddress = new("https://localhost"), AllowAutoRedirect = false
    });

    private async Task<string> FrontendTokenAsync(WebApplicationFactory<Program> app)
    {
        var email = $"mcp-{Guid.NewGuid():N}@example.test";
        await using var database = factory.Database.CreateContext();
        database.Users.Add(new() { Email = email, DisplayName = "MCP account" });
        await database.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Email, email), new(ClaimTypes.Name, email)], "Bearer"));
        return app.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme)
            .BearerTokenProtector.Protect(new(principal, new() { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) }, "Bearer"));
    }

    private static string AuthorizationUrl(string verifier, string redirect = Redirect) => QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
    {
        ["client_id"] = "chatgpt-test", ["redirect_uri"] = redirect, ["response_type"] = "code",
        ["scope"] = McpOptions.Scope + " offline_access", ["resource"] = Resource, ["state"] = "test-state",
        ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256"
    });

    private static async Task<string> AuthorizeAsync(HttpClient client, string frontend, string verifier)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync(AuthorizationUrl(verifier));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["mcp_request"].ToString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", frontend);
        client.DefaultRequestHeaders.Add("Origin", "https://frontend.example");
        response = await client.PostAsJsonAsync("/connect/approve", new { transactionId = id, approved = true });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var approval = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("Origin");
        response = await client.GetAsync(approval.GetProperty("redirectUrl").GetString());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        response = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.False(query.ContainsKey("error"), response.Headers.Location.ToString());
        Assert.Equal("test-state", query["state"].ToString());
        return query["code"].ToString();
    }

    private static Task<HttpResponseMessage> ExchangeAsync(HttpClient client, string code, string verifier) => client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = "chatgpt-test", ["grant_type"] = "authorization_code", ["code"] = code,
        ["redirect_uri"] = Redirect, ["code_verifier"] = verifier, ["resource"] = Resource
    }));

    private static async Task<JsonElement> CallAsync(HttpClient client, string token, string name, object arguments)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name, arguments } });
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        if (text.StartsWith("event:") || text.StartsWith("data:")) text = text.Split('\n').First(x => x.StartsWith("data:"))[5..].Trim();
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(json.TryGetProperty("error", out _), text);
        return json.GetProperty("result").Clone();
    }

    [PostgreSqlFact]
    public async Task Discovery_requires_authentication_and_catalog_exposes_only_readable_application_contracts()
    {
        using var app = App(); using var client = Client(app);
        var metadata = await client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp");
        Assert.Equal(Resource, metadata.GetProperty("resource").GetString());
        var discovery = await client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server");
        Assert.Contains("S256", discovery.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" })).StatusCode);
        var operations = app.Services.GetRequiredService<McpEndpointCatalog>().McpOperations.Values;
        Assert.Contains(operations, x => x.Api.RelativePath == "api/accounts/me" && x.ReadOnly);
        Assert.Contains(operations, x => x.Api.HttpMethod == "POST" && !x.ReadOnly);
        Assert.DoesNotContain(operations, x => x.Api.RelativePath!.Contains("Authentication") || x.Api.RelativePath.StartsWith("connect/"));
        Assert.DoesNotContain(operations, x => x.Api.RelativePath!.Contains("api/Group/Get") || x.Api.RelativePath.Contains("Game/Get") || x.Api.RelativePath.Contains("api/platform"));
        var models = app.Services.GetRequiredService<McpModelCatalog>().List();
        Assert.Contains("AccountIdentityDto", JsonSerializer.Serialize(models));
        Assert.DoesNotContain("Fantasoccer.TeamKey", JsonSerializer.Serialize(models));
    }

    [PostgreSqlFact]
    public async Task Login_consent_PKCE_refresh_and_revocation_work_and_MCP_calls_execute_the_real_controller()
    {
        using var app = App(); using var client = Client(app);
        var frontend = await FrontendTokenAsync(app);
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var code = await AuthorizeAsync(client, frontend, verifier);
        var response = await ExchangeAsync(client, code, verifier);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("access_token").GetString()!;
        var account = await CallAsync(client, access, "get_my_account", new { });
        Assert.False(account.TryGetProperty("isError", out var accountError) && accountError.GetBoolean(), account.ToString());
        var operation = app.Services.GetRequiredService<McpEndpointCatalog>().McpOperations.Values.Single(x => x.Api.RelativePath == "api/accounts/me").Tool.Name;
        var result = await CallAsync(client, access, operation, new { });
        Assert.False(result.TryGetProperty("isError", out var resultError) && resultError.GetBoolean(), result.ToString());
        Assert.Contains("200", result.GetProperty("content")[0].GetProperty("text").GetString());
        // The MCP access ticket must never work on ordinary HTTP endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/accounts/me")).StatusCode);
        // The application token cannot act as an MCP token.
        client.DefaultRequestHeaders.Authorization = new("Bearer", frontend);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 2, method = "tools/list" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "chatgpt-test", ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!, ["resource"] = Resource
        }));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new("Bearer", frontend);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/connect/disconnect", new { clientId = "chatgpt-test" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 3, method = "tools/list" })).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Codes_are_single_use_PKCE_is_verified_and_unsafe_redirects_are_rejected()
    {
        using var app = App(); using var client = Client(app);
        var frontend = await FrontendTokenAsync(app);
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var wrongCode = await AuthorizeAsync(client, frontend, verifier);
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(client, wrongCode, new string('x', 43))).StatusCode);
        var code = await AuthorizeAsync(client, frontend, verifier);
        var redemptions = await Task.WhenAll(ExchangeAsync(client, code, verifier), ExchangeAsync(client, code, verifier));
        Assert.Single(redemptions, x => x.IsSuccessStatusCode);
        Assert.Single(redemptions, x => x.StatusCode == HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(AuthorizationUrl(verifier, "https://evil.example/callback"))).StatusCode);
        foreach (var redirect in new[] { "javascript:alert(1)", "http://public.example/callback", "https://user:pass@client.example/callback", "https://client.example/callback#fragment" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/connect/register", new { redirect_uris = new[] { redirect } })).StatusCode);
    }

    // Independent IPs keep production rate limiters active without coupling tests to each other.
    private sealed class ClientAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8::" + Random.Shared.Next(1, 65000).ToString("x"));
                await continuation();
            });
            next(app);
        };
    }
}
