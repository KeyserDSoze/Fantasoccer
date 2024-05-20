using System.Security.Claims;
using System.Security.Cryptography;
using Fantasoccer.Business;
using Fantasoccer.Domain;
using Fantasoccer.Engine;
using Fantasoccer.Infrastructure;
using Fantasoccer.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Rystem.Authentication.Social;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
builder.Services.AddSocialLogin(x =>
{
    //x.Google.ClientId = builder.Configuration["SocialLogin:Google:ClientId"];
    //x.Google.ClientSecret = builder.Configuration["SocialLogin:Google:ClientSecret"];
    //x.Google.RedirectDomain = builder.Configuration["SocialLogin:Google:RedirectDomain"];
    x.Microsoft.ClientId = builder.Configuration["SocialLogin:Microsoft:ClientId"];
    x.Microsoft.ClientSecret = builder.Configuration["SocialLogin:Microsoft:ClientSecret"];
    x.Microsoft.RedirectDomain = builder.Configuration["SocialLogin:Microsoft:RedirectDomain"];
    //x.Facebook.ClientId = builder.Configuration["SocialLogin:Facebook:ClientId"];
    //x.Facebook.ClientSecret = builder.Configuration["SocialLogin:Facebook:ClientSecret"];
    //x.Facebook.RedirectDomain = builder.Configuration["SocialLogin:Facebook:RedirectDomain"];
},
x =>
{
    x.BearerTokenExpiration = TimeSpan.FromHours(1);
    x.RefreshTokenExpiration = TimeSpan.FromDays(100);
});
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireClaim(ClaimTypes.Email);
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
    await builder.Services.AddStorageInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
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
app.UseSocialLoginEndpoints();
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .SetPolicyForAll()
    .With("authenticated")
    .Build();
app.Run();
