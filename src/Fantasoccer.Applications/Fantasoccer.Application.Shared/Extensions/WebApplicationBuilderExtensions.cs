using Blazored.LocalStorage;
using Fantasoccer.Business;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Fantasoccer.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Fantasoccer.Application.Shared.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        public static async Task<WebApplicationBuilder> ConfigureServicesAsync(this WebApplicationBuilder builder)
        {
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
            builder.Services.AddSingleton<StaticSettings>();
            builder.Services.AddFluentUIComponents(options =>
            {
                options.ValidateClassNames = false;
            });
            builder.Services.AddBlazoredLocalStorage();
            builder.Services.AddHttpClientIntegrationForApi(builder.Configuration);
            await builder.Services.AddStorageForClientInfrastructuresAsync(builder.Configuration["ConnectionStrings:Storage"]!);
            builder.Services.AddSingleton(new AuctionHubClientSettings
            {
                ApiUri = $"wss://{builder.Configuration["Api:Uri"]}/auctionhub"
            });
            builder.Services.AddTransient<AuctionHubClient>();
            builder.Services.AddSharedBusiness();
            builder.Services.AddDefaultSocialLoginAuthorizationInterceptorForApiHttpClient();
            builder.Services.AddSocialLoginUI(x =>
            {
                x.ApiUrl = $"https://{builder.Configuration["Api:Uri"]}";
                //x.Google.ClientId = builder.Configuration["SocialLogin:Google:ClientId"];
                x.Microsoft.ClientId = builder.Configuration["SocialLogin:Microsoft:ClientId"];
            });
            return builder;
        }
    }
}
