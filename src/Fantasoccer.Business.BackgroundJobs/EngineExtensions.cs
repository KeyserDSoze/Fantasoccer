using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Engine
{
    public static class EngineExtensions
    {
        public static async Task<IServiceCollection> AddFantacalcioBackgroundWorkAsync(this IServiceCollection services)
        {
            services
                .AddBackgroundJob<PlayerOddsJob>(options =>
                {
                    options.Cron = "0 0 6,10,12,14,18,20 * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<SerieACalendarJob>(options =>
                {
                    options.Cron = "0 0 2,6,12,18 * * *";
                })
                .AddBackgroundJob<LiveVotesJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<FinalVotesJob>(options =>
                {
                    options.Cron = "0 45 2,7 * * *";
                })
                .AddBackgroundJob<AllPlayersJob>(options =>
                {
                    options.Cron = "0 0 5 * * *";
                    //options.RunImmediately = true;
                })
                .AddBackgroundJob<PlayerImagesJob>(options =>
                {
                    options.Cron = "0 0 1 9 *";
                })
                .AddBackgroundJob<SetFormationJob>(options =>
                {
                    options.Cron = "0 * * * *";
                });
            return services;
        }
    }
}
