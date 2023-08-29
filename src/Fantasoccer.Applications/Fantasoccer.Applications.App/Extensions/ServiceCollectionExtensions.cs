using Fantasoccer.Domain.SerieA;

namespace Fantasoccer.Applications.App.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpClientForApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClientIntegration<RealCalendar, int>(configuration);
            services.AddHttpClientIntegration<RealPlayersWrapper, int>(configuration);
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
            services.AddHttpClientIntegration<Rank, LeagueKey>(configuration);
            services.AddHttpClientIntegration<Calendar, LeagueKey>(configuration);
            services.AddHttpClientIntegration<PlayedGame, GameKey>(configuration);
            services.AddHttpClientIntegration<SerieATeamsWrapper, int>(configuration);
            services.AddHttpClientIntegration<Team, TeamKey>(configuration);
            services.AddHttpClientIntegration<Team, TeamDayKey>(configuration);
            services.AddHttpClientIntegration<AppIdentity, string>(configuration);
            return services;
        }
        private static IServiceCollection AddHttpClientIntegration<T, TKey>(this IServiceCollection services, IConfiguration configuration)
           where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder =>
            {
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                });
            });
        }
    }
}
