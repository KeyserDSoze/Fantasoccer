using Blazored.LocalStorage;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Fantasoccer.Applications.App;
using Fantasoccer.Applications.App.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Fantasoccer.Business;

var builder = WebApplication.CreateBuilder(args);
//var scopes = new string[] { builder.Configuration["AzureAdB2c:Scopes"]! };
//builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
//    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAdB2c"))
//    .EnableTokenAcquisitionToCallDownstreamApi(scopes)
//    .AddInMemoryTokenCaches();

builder.Services.AddApplicationInsightsTelemetry(x =>
{
    x.ConnectionString = builder.Configuration["ConnectionStrings:AppInsights"];
});
builder.Services.AddControllersWithViews();
    //.AddMicrosoftIdentityUI();
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpClientIntegrationForApi(builder.Configuration);
builder.Services.AddDefaultAuthorizationInterceptorForApiHttpClient();
//builder.Services.AddAuthorization(options =>
//{
//    options.FallbackPolicy = options.DefaultPolicy;
//});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
