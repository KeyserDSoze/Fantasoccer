using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Fantasoccer.Engine;
using Fantasoccer.Business;
using Fantasoccer.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2c"));
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiFromRepositoryFramework()
    .WithSwagger()
    .WithDefaultCorsWithAllOrigins();
builder.Services.AddFantasoccerBusiness();
builder.Services.AddInMemoryInfrastructures();
await builder.Services.AddFantacalcioBackgroundWorkAsync();
var app = builder.Build();
await app.Services.WarmUpAsync();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseApiFromRepositoryFramework()
    .WithNoAuthorization();
    //.SetPolicyForAll()
    //.Build();
app.Run();
