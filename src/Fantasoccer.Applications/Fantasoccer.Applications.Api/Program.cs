using Fantasoccer.Business;
using Fantasoccer.Engine;
using Fantasoccer.Infrastructure;
using Fantasoccer.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
     .AddMicrosoftIdentityWebApi(options =>
     {
         builder.Configuration.Bind("AzureAdB2c", options);
         options.TokenValidationParameters.NameClaimType = "name";
     },
    options =>
    {
        builder.Configuration.Bind("AzureAdB2c", options);
    });
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireClaim("name");
    });
});
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
    await builder.Services.AddStorageInfrastructuresAsync(builder.Configuration);
}
builder.Services.AddFantasoccerBusiness();
builder.Services.AddFantasoccerBackgroundWork();
builder.Services.AddSharedBusiness();
builder.Services.AddApiFromRepositoryFramework()
    .WithSwagger()
    .WithDefaultCorsWithAllOrigins();
if (isInMemory)
{
    builder.Services.AddDefaultValues(false);
}


var app = builder.Build();
await app.Services.WarmUpAsync();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .SetPolicyForAll()
    .With("authenticated")
    .Build();
app.Run();
