using System.Security.Claims;
using Fantasoccer.Business;
using Fantasoccer.Engine;
using Fantasoccer.Infrastructure;
using Fantasoccer.InMemory;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireClaim(ClaimTypes.Email);
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationInsightsTelemetry(x =>
{
    x.ConnectionString = builder.Configuration["ConnectionStrings:AppInsights"];
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
if (isInMemory)
{
    builder.Services.AddInMemoryInfrastructures();
    builder.Services.AddPreDefaultValues();
}
else
{
    await builder.Services.AddStorageInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
}
builder.Services.AddFantasoccerBusiness(builder.Configuration);
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
//builder.Services.AddSignalR();
//builder.Services.AddResponseCompression(opts =>
//{
//    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
//        ["application/octet-stream"]);
//});
if (isInMemory)
{
    builder.Services.AddDefaultValues(false);
}


var app = builder.Build();
await app.Services.WarmUpAsync();
//app.UseResponseCompression();
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
//app.UseEndpoints(x => x.MapHub<AuctionHub>("/auctionhub"));
app.Run();
