using Fantasoccer.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpClientIntegrationForApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<ApiService>();
            services.AddHttpClient("Identity", x =>
            {
                var uri = configuration["Api:Uri"];
                if (uri != null && !uri.StartsWith("http"))
                    uri = $"https://{uri}";
                x.BaseAddress = new Uri($"{uri}/Authentication/Token");
            });
            services.AddScoped<IdentityService>();
            services.AddHttpClientIntegration<RealCalendar, int>(configuration);
            services.AddHttpClientIntegration<RealPlayersWrapper, int>(configuration);
            services.AddHttpClientIntegration<RealPlayersWrapper, LeagueKey>(configuration, "random");
            services.AddHttpClientIntegration<ChancedRealPlayerWrapper, YearDayKey>(configuration);
            services.AddRepository<VotedRealPlayerWrapper, YearDayKey>(builder =>
            {
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    apiBuilder.WithServerFactoryName("live");
                }, "live");
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    apiBuilder.WithServerFactoryName("official");
                }, "official");
            });
            services.AddHttpClientIntegration<Group, string>(configuration);
            services.AddHttpClientIntegration<LiveGroup, string>(configuration);
            services.AddHttpClientIntegration<Rank, LeagueKey>(configuration);
            services.AddHttpClientIntegration<Rank, YearDayLeague>(configuration, "day");
            services.AddHttpClientIntegration<Calendar, LeagueKey>(configuration);
            services.AddHttpClientIntegration<MarketWrapper, LeagueKey>(configuration);
            services.AddHttpClientIntegration<PlayedGame, GameKey>(configuration);
            services.AddHttpClientIntegration<RealTeamWrapper, int>(configuration);
            services.AddHttpClientIntegration<StatPlayersWrapper, int>(configuration);
            services.AddHttpClientIntegration<Team, TeamKey>(configuration);
            services.AddHttpClientIntegration<Team, TeamDayKey>(configuration, "day");
            services.AddHttpClientIntegration<NewsWrapper, YearDayGroup>(configuration, "news");
            services.AddHttpClientIntegration<PushNotificationSettings, string>(configuration);
            services.AddHttpClientIntegration<AppIdentity, string>(configuration);
            services.AddHttpClientIntegration<HallOfFame, GeneralLeagueKey>(configuration);
            return services;
        }
        private static IServiceCollection AddHttpClientIntegration<T, TKey>(this IServiceCollection services,
            IConfiguration configuration,
            string? factoryName = null)
           where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder =>
            {
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    if (factoryName != null)
                        apiBuilder.WithServerFactoryName(factoryName);
                }, factoryName, ServiceLifetime.Transient);
            });
        }
    }
}
