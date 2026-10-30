using System.Diagnostics;
using System.Net;
using Fantasoccer.Application.Api.LocalDevelopment;
using Fantasoccer.Application.Api.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Api;

public sealed class LocalSafetyTests
{
    [Fact]
    public async Task Local_profile_blocks_external_data_fetching_and_push_without_modifying_subscriptions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalHttp:Enabled"] = "false", ["Push:Enabled"] = "false"
        });
        builder.AddLocalSafetyControls();
        await using var services = builder.Services.BuildServiceProvider();
        var clients = services.GetRequiredService<IHttpClientFactory>();
        foreach (var name in new[] { "base", "player-images", "seriea", "all-players" })
            await Assert.ThrowsAsync<HttpRequestException>(() => clients.CreateClient(name).GetAsync("https://must-not-be-called.invalid"));
    }

    [Fact]
    public void Telemetry_redacts_query_strings_userinfo_and_sql()
    {
        using var span = new Activity("example").Start();
        span.SetTag("url.full", "https://private:password@example.test/container/blob?sig=private-token#fragment");
        span.SetTag("http.url", "https://example.test/Token?code=secret&client_secret=secret");
        span.SetTag("url.query", "access_token=secret");
        span.SetTag("http.target", "/Token?code=secret");
        span.SetTag("db.statement", "SELECT 'personal data'");
        span.SetTag("db.query.text", "SELECT 'personal data'");
        new SensitiveSpanProcessor().OnEnd(span);
        Assert.Equal("https://example.test/container/blob", span.GetTagItem("url.full"));
        Assert.Equal("https://example.test/Token", span.GetTagItem("http.url"));
        foreach (var name in new[] { "url.query", "http.target", "db.statement", "db.query.text" }) Assert.Null(span.GetTagItem(name));
    }
}
