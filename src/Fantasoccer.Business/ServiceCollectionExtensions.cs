using Fantasoccer.Business.Authorization;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFantasoccerBusiness(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSocialLogin<SocialUserProvider>(x =>
            {
                //x.Google.ClientId = builder.Configuration["SocialLogin:Google:ClientId"];
                //x.Google.ClientSecret = builder.Configuration["SocialLogin:Google:ClientSecret"];
                //x.Google.RedirectDomain = builder.Configuration["SocialLogin:Google:RedirectDomain"];
                x.Microsoft.ClientId = configuration["SocialLogin:Microsoft:ClientId"];
                x.Microsoft.ClientSecret = configuration["SocialLogin:Microsoft:ClientSecret"];
                x.Microsoft.AddUris(configuration["SocialLogin:Microsoft:RedirectDomains"]!.Split(','));
                //x.Facebook.ClientId = builder.Configuration["SocialLogin:Facebook:ClientId"];
                //x.Facebook.ClientSecret = builder.Configuration["SocialLogin:Facebook:ClientSecret"];
                //x.Facebook.RedirectDomain = builder.Configuration["SocialLogin:Facebook:RedirectDomain"];
            },
            x =>
            {
                x.BearerTokenExpiration = TimeSpan.FromHours(1);
                x.RefreshTokenExpiration = TimeSpan.FromDays(100);
            });
            services.AddFactory<IFormationParser, Fantagazzetta>(nameof(Fantagazzetta));
            services.AddFactory<IFormationParser, Gazzetta>(nameof(Gazzetta));
            services.AddFactory<IFormationParser, Injury>(nameof(Injury));
            //services.AddFactory<IFormationParser, Mediaset>(nameof(Mediaset));
            //services.AddFactory<IFormationParser, Sky>(nameof(Sky));
            services.AddTransient<IAllPlayers, FantagazzettaAllPlayers>();
            services.AddTransient<ILiveVote, LiveVote>();
            //services.AddTransient<ILiveVote, LiveVoteWithHtml>();
            services.AddTransient<IPushNotificationService, PushNotificationService>();
            services.AddHttpClient("base", client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            });
            services.AddHttpClient("seriea", client =>
            {
                client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.legaseriea.it/it");
            });
            services.AddHttpClient("seriea-gazzetta-calendar", client =>
            {
                client.BaseAddress = new Uri("https://api2-mtc.gazzetta.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.gazzetta.it/calcio/serie-a/calendario-risultati/");
            });
            services.ScanAuthorizationForRepositoryFramework();
            return services;
        }
    }
}
