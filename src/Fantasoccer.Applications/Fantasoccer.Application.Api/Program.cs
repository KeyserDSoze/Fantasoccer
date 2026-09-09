using System.Security.Claims;
using Fantasoccer.Application.Api.Hubs;
using Fantasoccer.Application.Api.Controllers;
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
    builder.Services.AddRepository<PushNotificationSettings, string>(x => x.WithInMemory());
    builder.Services.AddRepository<PushNotificationSent, YearDayKey>(x => x.WithInMemory());
    builder.Services.AddRepository<RealRank, int>(x => x.WithInMemory());
    builder.Services.AddRepository<LiveGroup, string>(x => x.WithInMemory());
    builder.Services.AddRepository<Rank, YearDayLeague>(x => x.WithInMemory());
    builder.Services.AddRepository<HallOfFame, GeneralLeagueKey>(x => x.WithInMemory());
    builder.Services.AddRepository<CardWrapper, string>(x => x.WithInMemory());
    builder.Services.AddPreDefaultValues();
}
else
{
    await builder.Services.AddStorageInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
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
            .WithOrigins("http://localhost:3000", "https://fanta.zone") // no trailing slash, include scheme
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

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing"))
    await app.Services.WarmUpAsync();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors("DefaultCors");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSocialLoginEndpoints();
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .SetPolicyForAll()
    .With("authenticated")
    .Build();
app.MapHub<AuctionHub>("/auctionhub");
app.Run();

public partial class Program;
