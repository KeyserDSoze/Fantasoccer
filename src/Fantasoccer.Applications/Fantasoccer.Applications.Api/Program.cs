using System.Security.Claims;
using Fantasoccer;
using Fantasoccer.Business;
using Fantasoccer.Engine;
using Fantasoccer.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using RepositoryFramework;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInMemoryInfrastructures();
builder.Services.AddFantasoccerBusiness();
builder.Services.AddDefaultValues();
builder.Services.AddFantasoccerBackgroundWork();
builder.Services.AddApiFromRepositoryFramework()
    .WithSwagger()
    .WithDefaultCorsWithAllOrigins();

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
