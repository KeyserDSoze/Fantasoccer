using Fantasoccer.Business.Authorization;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFantasoccerBusiness(this IServiceCollection services)
        {
            services.AddSocialUserProvider<SocialUserProvider>();
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
            services.ScanAuthorizationForRepositoryFramework();
            return services;
        }
    }
}
