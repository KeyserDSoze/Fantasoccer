using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Api.Mcp;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class WorkspaceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlFixture Database { get; } = new();
    public ValueTask InitializeAsync() => Database.InitializeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        using var context = Database.CreateContext();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Integrations:Infrastructure", "InMemory");
        builder.UseSetting("ConnectionStrings:PostgreSql", context.Database.GetConnectionString());
        builder.UseSetting("Mcp:Enabled", "false");
        builder.UseSetting("Telemetry:Provider", "None");
        builder.UseSetting("Http:RedirectToHttps", "false");
        builder.ConfigureServices(services => services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "RelationalTest";
            options.DefaultChallengeScheme = "RelationalTest";
        }).AddScheme<AuthenticationSchemeOptions, WorkspaceTestAuthentication>("RelationalTest", _ => { }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
    }

    public async Task<HttpClient> AccountAsync(bool globalAdmin = false)
    {
        var email = $"friend-{Guid.NewGuid():N}@example.test";
        await using var database = Database.CreateContext();
        database.Users.Add(new() { Email = email, DisplayName = "Friend" });
        await database.SaveChangesAsync();
        var client = CreateClient();
        if (globalAdmin)
        {
            var administrators = Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value;
            administrators.Emails = administrators.Emails.Append(email).ToArray();
        }
        client.DefaultRequestHeaders.Add("X-Workspace-User", email);
        if (globalAdmin) client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        return client;
    }
}

internal sealed class WorkspaceTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Workspace-User", out var email)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.Email, email.ToString()), new(ClaimTypes.Name, email.ToString()) };
        if (Request.Headers["X-Workspace-Admin"] == "true") claims.Add(new(ClaimTypes.Role, "super_admin"));
        if (Request.Headers["X-Workspace-Mcp"] == "true") claims.Add(new(McpOptions.ResourceClaim, "test-resource"));
        return Task.FromResult(AuthenticateResult.Success(new(new(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}

public sealed partial class WorkspaceApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Public_health_and_protected_workspace_have_distinct_authorization()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/workspace/account")).StatusCode);
        using var ordinary = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/workspace/groups", new { name = "Forbidden creation" })).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Legacy_document_endpoints_do_not_exist_and_MCP_tokens_stay_resource_bound()
    {
        using var client = await factory.AccountAsync(globalAdmin: true);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Group/Get?key=legacy-group")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Game/Get?groupId=legacy-group")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Workspace-Mcp", "true");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/accounts/me")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Frontend_contract_flow_creates_shared_basket_leagues_and_team_without_compact_fields()
    {
        using var client = await factory.AccountAsync(globalAdmin: true);
        var account = await client.GetFromJsonAsync<AccountSummaryDto>("/api/workspace/account");
        var response = await client.PostAsJsonAsync("/api/workspace/groups", new { name = "Friends" });
        response.EnsureSuccessStatusCode();
        var group = await response.Content.ReadFromJsonAsync<GroupSummaryDto>();
        var path = $"/api/workspace/groups/{group!.GroupId}";
        var basketResponse = await client.PostAsJsonAsync(path + "/baskets", new { name = "Shared basket", seasonIndex = 15 });
        basketResponse.EnsureSuccessStatusCode();
        var basket = await basketResponse.Content.ReadFromJsonAsync<BasketCreatedDto>();
        foreach (var name in new[] { "League", "Cup" })
            (await client.PostAsJsonAsync(path + "/leagues", new { name, seasonIndex = 15, basketIds = new[] { basket!.BasketId } })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path + "/teams", new
        {
            basketId = basket!.BasketId, seasonIndex = 15, name = "Team", ownerUserId = account!.UserId, additionalOwnerUserIds = Array.Empty<Guid>()
        })).EnsureSuccessStatusCode();
        var json = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Friends", json.GetProperty("name").GetString());
        Assert.Equal(2, json.GetProperty("leagues").GetArrayLength());
        Assert.Equal(1, json.GetProperty("baskets").GetArrayLength());
        Assert.True(json.GetProperty("teams")[0].TryGetProperty("seasonIndex", out _));
        Assert.False(json.TryGetProperty("g", out _));
        using var outsider = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PatchAsJsonAsync(path, new { name = "Forged" })).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Mcp_adapter_reads_the_same_group_as_HTTP_and_cannot_bypass_resource_authorization()
    {
        using var client = await factory.AccountAsync(globalAdmin: true);
        var account = await client.GetFromJsonAsync<AccountSummaryDto>("/api/workspace/account");
        var created = await (await client.PostAsJsonAsync("/api/workspace/groups", new { name = "MCP parity" })).Content.ReadFromJsonAsync<GroupSummaryDto>();
        var http = await client.GetFromJsonAsync<GroupDetailsDto>($"/api/workspace/groups/{created!.GroupId}");
        await using var scope = factory.Services.CreateAsyncScope();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new Claim(ClaimTypes.Email, account!.Email)], "test"))
        } };
        var tools = new McpWorkspaceTools(scope.ServiceProvider.GetRequiredService<GroupApplicationService>(), accessor);
        var mcp = await tools.Get(created.GroupId, default);
        Assert.Equal(http!.GroupId, mcp.GroupId);
        Assert.Equal(http.Name, mcp.Name);
        Assert.Equal(http.Roles, mcp.Roles);
        accessor.HttpContext.User = new(new ClaimsIdentity([new Claim(ClaimTypes.Email, "outside@example.test")], "test"));
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.Get(created.GroupId, default));
    }

    [PostgreSqlFact]
    public async Task Role_demotion_takes_effect_on_next_request_and_problem_details_are_typed()
    {
        using var creator = await factory.AccountAsync(globalAdmin: true);
        using var member = await factory.AccountAsync();
        var memberAccount = await member.GetFromJsonAsync<AccountSummaryDto>("/api/workspace/account");
        var created = await (await creator.PostAsJsonAsync("/api/workspace/groups", new { name = "Demotion group" })).Content.ReadFromJsonAsync<GroupSummaryDto>();
        var path = $"/api/workspace/groups/{created!.GroupId}";
        (await creator.PutAsJsonAsync(path + "/members", new { email = memberAccount!.Email, displayName = "Friend", roles = 4 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await member.PatchAsJsonAsync(path, new { name = "Allowed before demotion" })).StatusCode);
        (await creator.PutAsJsonAsync(path + "/members", new { email = memberAccount.Email, displayName = "Friend", roles = 2 })).EnsureSuccessStatusCode();
        var forbidden = await member.PatchAsJsonAsync(path, new { name = "Not allowed" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var problem = await forbidden.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("group_access_denied", problem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync(path)).StatusCode);
    }
}
