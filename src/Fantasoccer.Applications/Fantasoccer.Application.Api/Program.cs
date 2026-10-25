using System.Security.Claims;
using Fantasoccer.Application.Api.Hubs;
using Fantasoccer.Application.Api.Controllers;
using Fantasoccer.Application.Api.Mcp;
using Fantasoccer.Business;
using Fantasoccer.Engine;
using Fantasoccer.Infrastructure;
using Fantasoccer.InMemory;
using Fantasoccer.Domain;
using Fantasoccer;
using Microsoft.AspNetCore.ResponseCompression;
using RepositoryFramework;
using RepositoryFramework.InMemory;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
builder.Logging.AddFilter("Fantasoccer", LogLevel.Information);
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireClaim(ClaimTypes.Email);
    });
    configure.AddPolicy("super_admin", policy =>
    {
        policy.RequireClaim(ClaimTypes.Role, "super_admin");
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationInsightsTelemetry(x =>
{
    x.ConnectionString = builder.Configuration["ConnectionStrings:AppInsights"];
});
builder.Services.AddControllers();
builder.Services.AddTransient<OpeningCompetitionController>();
builder.Services.AddEndpointsApiExplorer();
if (isInMemory)
{
    builder.Services.AddInMemoryInfrastructures();
    builder.Services.AddRepository<AuctionWrapper, string>(x => x.WithInMemory());
    builder.Services.AddRepository<PushNotificationSettings, string>(x => { x.WithInMemory(); x.SetNotExposable(); });
     builder.Services.AddRepository<Settings, string>(x => x.WithInMemory());
    builder.Services.AddRepository<PushNotificationSent, YearDayKey>(x => { x.WithInMemory(); x.SetNotExposable(); });
    builder.Services.AddRepository<RealRank, int>(x => x.WithInMemory());
    builder.Services.AddRepository<LiveGroup, string>(x => x.WithInMemory());
    // Match the production factory: daily ranks must not replace the default
    // seasonal Rank<LeagueKey> repository while running regression tests.
    builder.Services.AddRepository<Rank, YearDayLeague>(x => x.WithInMemory(name: "day"));
    builder.Services.AddRepository<HallOfFame, GeneralLeagueKey>(x => x.WithInMemory());
    builder.Services.AddRepository<CardWrapper, string>(x => x.WithInMemory());
    builder.Services.AddPreDefaultValues();
}
else
{
    await builder.Services.AddStorageInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
    if (builder.Configuration.GetValue<bool>("Mcp:Enabled"))
        await builder.Services.AddOAuthBlobStorageAsync(builder.Configuration["ConnectionStrings:Storage"]!);
}
builder.Services.AddFantasoccerBusiness(builder.Configuration);
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddFantasoccerBackgroundWork();
builder.Services.AddSharedBusiness();
builder.Services.AddApiFromRepositoryFramework()
    .WithSwagger()
    .WithDefaultCorsWithAllOrigins();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors",
        policy => policy
            .WithOrigins(new[] { "http://localhost:3000", "https://fanta.zone" }
                .Concat(Uri.TryCreate(builder.Configuration["Mcp:FrontendUrl"], UriKind.Absolute, out var frontend)
                    ? [frontend.GetLeftPart(UriPartial.Authority)] : Array.Empty<string>()).Distinct().ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});
if (isInMemory)
{
    builder.Services.AddDefaultValues(false);
}
builder.Services.AddLock();
builder.AddFantasoccerMcp();

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing"))
    await app.Services.WarmUpAsync();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors("DefaultCors");
app.UseRouting();
if (app.Services.GetRequiredService<McpOptions>().Enabled)
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TeamRepositoryBoundaryMiddleware>();
app.UseMiddleware<McpResourceBoundaryMiddleware>();
if (app.Services.GetRequiredService<McpOptions>().Enabled)
{
    app.UseMiddleware<McpOriginBoundaryMiddleware>();
}
app.UseSocialLoginEndpoints();
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .SetPolicyForAll()
    .With("authenticated")
    .Build();
app.MapHub<AuctionHub>("/auctionhub");
await app.MapFantasoccerMcpAsync();
app.Run();

public partial class Program;
