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
using Fantasoccer.Application.Api.Observability;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Fantasoccer.Application.Api.LocalDevelopment;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Api.Authentication;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Application.Api.Storage;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
var postgresEnabled = builder.Configuration.GetValue<bool>("PostgreSql:Enabled");
builder.Logging.AddFilter("Fantasoccer", LogLevel.Information);
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(ClaimTypes.Email);
    });
    configure.AddPolicy("super_admin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(ClaimTypes.Role, "super_admin");
    });
});
builder.Services.AddHttpContextAccessor();
builder.AddFantasoccerTelemetry();
var healthChecks = builder.Services.AddHealthChecks();
if (postgresEnabled)
{
    builder.Services.AddFantasoccerPostgreSql(builder.Configuration.GetConnectionString("PostgreSql")
        ?? throw new InvalidOperationException("PostgreSql requires ConnectionStrings:PostgreSql."));
    healthChecks.AddCheck<PostgreSqlReadinessCheck>("postgresql", tags: ["ready"]);
    builder.Services.AddGroupApplication();
    builder.Services.Configure<Fantasoccer.Application.Core.Caching.SharedCacheOptions>(builder.Configuration.GetSection("Cache:Shared"));
    // Safety window after a concluded matchday before its push details are removed.
    builder.Services.Configure<Fantasoccer.Application.Core.Notifications.PushRetentionOptions>(builder.Configuration.GetSection("PushRetention"));
    if (isInMemory) Fantasoccer.Infrastructure.Caching.DistributedCacheServiceCollectionExtensions.AddInMemoryCoordinatedDistributedCache(builder.Services);
    else Fantasoccer.Infrastructure.Caching.DistributedCacheServiceCollectionExtensions.AddAzureBlobDistributedCache(builder.Services, options =>
    {
        var section = builder.Configuration.GetSection(Fantasoccer.Infrastructure.Caching.AzureBlobDistributedCacheOptions.SectionName);
        section.Bind(options);
        // Same storage account as the files, separate private container. The
        // namespace keeps environments apart when they share an account.
        if (string.IsNullOrWhiteSpace(options.ConnectionString)) options.ConnectionString = builder.Configuration.GetConnectionString("Storage");
        options.Namespace = section["Namespace"] is { Length: > 0 } configured ? configured : builder.Environment.EnvironmentName.ToLowerInvariant();
    });
    builder.Services.AddOptions<Fantasoccer.Application.Core.Editorial.EditorialAiOptions>().Bind(builder.Configuration.GetSection("OpenAi"))
        .PostConfigure(options => options.Enabled &= builder.Configuration.GetValue("ExternalHttp:Enabled", true))
        .Validate(options => !options.Enabled || (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
            !string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.ModelName)), "AI generation requires a configured HTTPS endpoint, private key and model.")
        .ValidateOnStart();
    builder.Services.AddScoped<Fantasoccer.Application.Core.Editorial.IEditorialTextGenerator, Fantasoccer.Application.Api.Editorial.AzureEditorialTextGenerator>();
    if (builder.Configuration.GetValue("BackgroundJobs:Enabled", false)) builder.Services.AddHostedService<Fantasoccer.Application.Api.Operations.SqlJobsWorker>();
    builder.Services.AddOptions<PushTransportOptions>().Bind(builder.Configuration.GetSection("Push"))
        .Validate(options => !options.Enabled || options.HasValidConfiguration(), "Push requires valid matching VAPID keys and a mailto/HTTPS subject from private configuration.").ValidateOnStart();
    builder.Services.AddHttpClient("sql-web-push", client => client.Timeout = TimeSpan.FromSeconds(15))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    builder.Logging.AddFilter("System.Net.Http.HttpClient.sql-web-push", LogLevel.None);
    builder.Services.AddScoped<Fantasoccer.Application.Core.Notifications.IPushDeliveryTransport, Fantasoccer.Application.Api.Notifications.WebPushDeliveryTransport>();
    if (builder.Configuration.GetValue("Push:Enabled", false)) builder.Services.AddHostedService<Fantasoccer.Application.Api.Notifications.SqlPushWorker>();
    builder.Services.Configure<GlobalAdministratorOptions>(options =>
        options.Emails = builder.Configuration.GetSection("Authorization:GlobalAdministrators").Get<string[]>() ?? []);
    builder.Services.AddScoped<WorkspaceFaultFilter>();
    builder.Services.AddOptions<AssetLinkOptions>().Bind(builder.Configuration.GetSection("Assets"))
        .Validate(options => Uri.TryCreate(options.PublicBlobBaseUrl, UriKind.Absolute, out var uri) &&
            string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
            (uri.Scheme == "https" || ((builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")) && uri.IsLoopback && uri.Scheme == "http")),
            "Assets:PublicBlobBaseUrl must be a public HTTPS base URL without credentials or query parameters (loopback HTTP is allowed locally).")
        .ValidateOnStart();
    builder.Services.AddScoped<TeamAssetsApplicationService>();
    builder.Services.AddScoped<Fantasoccer.Application.Core.Auctions.IAuctionUpdatePublisher, SignalRAuctionUpdatePublisher>();
    if (isInMemory) builder.Services.AddSingleton<Fantasoccer.Application.Core.Cards.ICardImageStorage, InMemoryCardImageStorage>();
    else builder.Services.AddSingleton<Fantasoccer.Application.Core.Cards.ICardImageStorage, BlobCardImageStorage>();
    if (isInMemory) builder.Services.AddSingleton<ITeamLogoStorage, InMemoryTeamLogoStorage>();
    else builder.Services.AddSingleton<ITeamLogoStorage, BlobTeamLogoStorage>();
}
builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
    manager.FeatureProviders.Add(new WorkspaceControllerFeatureProvider(postgresEnabled)));
if (!postgresEnabled) builder.Services.AddTransient<OpeningCompetitionController>();
builder.Services.AddEndpointsApiExplorer();
if (postgresEnabled)
{
    if (isInMemory)
    {
        foreach (var name in new[] { "images", "logos", "cards" }) builder.Services.AddContentRepository().WithInMemoryIntegration(name);
    }
    else
    {
        var storage = builder.Configuration.GetConnectionString("Storage") ?? throw new InvalidOperationException("Blob file storage requires ConnectionStrings:Storage.");
        await builder.Services.AddFileStorageAsync(storage);
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Mcp:DataProtectionBlobName"]))
            await Fantasoccer.Infrastructure.ServiceCollectionExtensions.ValidatePrivateKeyRingAsync(storage);
    }
    builder.Services.AddFantasoccerRuleEngines();
    builder.Services.AddFantasoccerSqlDataProviders();
    builder.Services.AddSocialLogin<PostgreSqlSocialUserProvider>(login =>
    {
        login.Microsoft.ClientId = builder.Configuration["SocialLogin:Microsoft:ClientId"];
        login.Microsoft.ClientSecret = builder.Configuration["SocialLogin:Microsoft:ClientSecret"];
        login.Microsoft.AddUris(builder.Configuration["SocialLogin:Microsoft:RedirectDomains"]!.Split(','));
    }, lifetime => { lifetime.BearerTokenExpiration = TimeSpan.FromHours(24); lifetime.RefreshTokenExpiration = TimeSpan.FromDays(100); });
    builder.Services.AddSingleton<IOAuthDocumentStorage, Fantasoccer.Application.Api.Mcp.Storage.PostgreSqlOAuthDocumentStorage>();
    builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
    builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions>(Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenDefaults.AuthenticationScheme, options =>
    {
        var previous = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            if (previous != null) await previous(context);
            if (context.Result != null || !string.IsNullOrEmpty(context.Token) || context.Request.Headers.ContainsKey("Authorization")) return;
            // Browser WebSocket/SSE transports cannot set Authorization headers.
            // A query token is accepted only on the hub, never on controllers.
            if (context.Request.Path.StartsWithSegments("/auctionhub") && context.Request.Query.TryGetValue("access_token", out var token) && token.Count == 1 && token[0] is { Length: > 0 and <= 16384 } value)
                context.Token = value;
        };
    });
    builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name));
}
else if (isInMemory)
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
    if (!postgresEnabled && builder.Configuration.GetValue<bool>("Mcp:Enabled"))
        await builder.Services.AddOAuthBlobStorageAsync(builder.Configuration["ConnectionStrings:Storage"]!);
}
if (!postgresEnabled)
{
    builder.Services.AddFantasoccerBusiness(builder.Configuration);
    if (!builder.Environment.IsEnvironment("Testing") && builder.Configuration.GetValue("BackgroundJobs:Enabled", true))
        builder.Services.AddFantasoccerBackgroundWork();
    builder.Services.AddSharedBusiness();
    builder.Services.AddApiFromRepositoryFramework().WithSwagger().WithDefaultCorsWithAllOrigins();
}
builder.AddLocalSafetyControls();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors",
        policy => policy
             .WithOrigins(new[] { "http://localhost:3000", "https://fanta.zone" }
                .Concat(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
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
if (isInMemory && !postgresEnabled)
{
    builder.Services.AddDefaultValues(false);
}
builder.Services.AddLock();
builder.AddFantasoccerMcp();

var app = builder.Build();
if (postgresEnabled)
    await app.Services.MigrateFantasoccerDatabaseAsync();
if (!postgresEnabled && !app.Environment.IsEnvironment("Testing") && builder.Configuration.GetValue("Startup:WarmUpEnabled", true))
    await app.Services.WarmUpAsync();
app.UseResponseCompression();
if (builder.Configuration.GetValue("Http:RedirectToHttps", true))
    app.UseHttpsRedirection();
app.UseCors("DefaultCors");
app.UseRouting();
app.Use(async (context, next) =>
{
    await next();
    if (!context.Request.Path.StartsWithSegments("/health"))
        app.Logger.LogInformation("HTTP {Method} {Endpoint} responded {StatusCode}", context.Request.Method,
            context.GetEndpoint()?.DisplayName ?? "unmatched", context.Response.StatusCode);
});
if (app.Services.GetRequiredService<McpOptions>().Enabled)
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (!postgresEnabled)
{
    app.UseMiddleware<TeamRepositoryBoundaryMiddleware>();
}
app.UseMiddleware<McpResourceBoundaryMiddleware>();
if (app.Services.GetRequiredService<McpOptions>().Enabled)
{
    app.UseMiddleware<McpOriginBoundaryMiddleware>();
}
app.UseSocialLoginEndpoints();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
if (postgresEnabled) { app.UseSwagger(); app.UseSwaggerUI(); app.MapHub<RelationalAuctionHub>("/auctionhub"); }
else
{
    app.UseApiFromRepositoryFramework().SetPolicyForAll().With("authenticated").Build();
    app.MapHub<AuctionHub>("/auctionhub");
}
await app.MapFantasoccerMcpAsync();
app.Run();

public partial class Program;
