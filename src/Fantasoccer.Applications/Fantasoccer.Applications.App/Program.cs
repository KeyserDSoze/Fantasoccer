using Blazored.LocalStorage;
using Fantasoccer.Applications.App;
using Fantasoccer.Applications.App.ClientServices;
using Fantasoccer.Applications.App.Extensions;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry(x =>
{
    x.ConnectionString = builder.Configuration["ConnectionStrings:AppInsights"];
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddTransient<UiUtilityService>();
builder.Services.AddSingleton<ImageUriHelper>();
builder.Services.AddSingleton<VersionService>();
builder.Services.AddFluentUIComponents(options =>
{
    options.ValidateClassNames = false;
});
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpClientIntegrationForApi(builder.Configuration);
await builder.Services.AddStorageForClientInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
builder.Services.AddDefaultAuthorizationInterceptorForApiHttpClient();
//builder.Services.AddAuthorization(options =>
//{
//    options.FallbackPolicy = options.DefaultPolicy;
//});
builder.Services.AddSingleton(new AuctionHubClientSettings
{
    ApiUri = $"wss://{builder.Configuration["Api:Uri"]}/auctionhub"
});
builder.Services.AddTransient<AuctionHubClient>();
builder.Services.AddSocialLoginUI(x =>
{
    x.ApiUrl = $"https://{builder.Configuration["Api:Uri"]}";
    //x.Google.ClientId = builder.Configuration["SocialLogin:Google:ClientId"];
    x.Microsoft.ClientId = builder.Configuration["SocialLogin:Microsoft:ClientId"];
});
//.AddMicrosoftIdentityConsentHandler();
//builder.Services.AddDefaultAuthorizationInterceptorForApiHttpClient<TokenManager>(settings =>
//{
//    settings.Scopes = scopes;
//});
builder.Services.AddSharedBusiness();
builder.Services.AddFantasoccerBusinessForApp();

var app = builder.Build();
await app.Services.WarmUpAsync();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
