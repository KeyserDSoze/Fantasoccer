using Fantasoccer.Application.Api.Mcp;
using Microsoft.AspNetCore.Builder;
using OpenIddict.Abstractions;

namespace Fantasoccer.Tests.Tests.Api;

public sealed class McpConfigurationTests
{
    [Fact]
    public void Disabled_feature_does_not_register_OAuth_or_require_secrets()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Mcp:Enabled"] = "false";
        builder.AddFantasoccerMcp();
        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(IOpenIddictApplicationManager));
        Assert.Contains(builder.Services, x => x.ImplementationInstance is McpOptions { Enabled: false });
    }

    [Fact]
    public void Enabled_feature_fails_closed_when_configuration_is_incomplete()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Issuer"] = "http://insecure.example";
        Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
    }

    [Fact]
    public void Production_never_accepts_volatile_OAuth_storage()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Integrations:Infrastructure"] = "InMemory";
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
        Assert.Contains("volatile", exception.Message);
    }

    [Fact]
    public void Duplicate_client_configuration_is_rejected_before_seeding()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration["Mcp:Enabled"] = "true";
        builder.Configuration["Mcp:Clients:0:ClientId"] = "duplicate";
        builder.Configuration["Mcp:Clients:1:ClientId"] = "duplicate";
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddFantasoccerMcp());
        Assert.Contains("unique", exception.Message);
    }
}
