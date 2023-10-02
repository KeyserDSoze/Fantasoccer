using System.Security.Cryptography;
using Fantasoccer.Business;
using Fantasoccer.Domain;
using Fantasoccer.Engine;
using Fantasoccer.Infrastructure;
using Fantasoccer.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var isInMemory = builder.Configuration["Integrations:Infrastructure"] == "InMemory";
builder.Services.AddSingleton(new IdentityOptions
{
    ClientId = builder.Configuration["AzureAdB2c:ClientId"]!,
    Domain = builder.Configuration["AzureAdB2c:Domain"]!,
    Instance = builder.Configuration["AzureAdB2c:Instance"]!,
    Audience = builder.Configuration["AzureAdB2c:Audience"]!,
    Policy = builder.Configuration["AzureAdB2c:SignUpSignInPolicyId"]!,
    ExpiringInMinutes = int.Parse(builder.Configuration["Options:ExpiringInMinutes"]!)
});
var securityOptions = await builder.Services.AddSecurityOptionsAsync(builder.Configuration["ConnectionStrings:Storage"]!);
var rsa = new RSACryptoServiceProvider(2048);
rsa.ImportParameters(securityOptions.Rsa.ToParameters());
RsaSecurityKey rsaSecurityKey = new(rsa);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = rsaSecurityKey,
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };
});
builder.Services.AddAuthorization(configure =>
{
    configure.AddPolicy("authenticated", policy =>
    {
        policy.RequireClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
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
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .SetPolicyForAll()
    .With("authenticated")
    .Build();
app.Run();
