using FantacalcioV4.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace FantacalcioV4.Engine
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
                .AddBackgroundJob<DailyRestoringJob>(options =>
                {
                    options.Cron = "0 35 7 * * *";
                    //options.RunImmediately = true;
                })
                .AddBackgroundJob<CreateNewYearJob>(options =>
                {
                    options.Cron = "0 0 20-25 8 *";
                    //options.RunImmediately = true;
                })
                .AddBackgroundJob<PlayerImagesJob>(options =>
                {
                    options.Cron = "0 0 1 9 *";
                })
                .AddBackgroundJob<SetFormationJob>(options =>
                {
                    options.Cron = "0 * * * *";
                })
                .AddBackgroundJob<RestoreActualLiveJob>(options =>
                {
                    options.Cron = "*/4 * * * *";
                    options.RunImmediately = true;
                });
            services.AddHttpClient("base", client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            });
            return services;
        }
    }
}