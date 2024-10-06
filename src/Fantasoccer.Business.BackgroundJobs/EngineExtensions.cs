using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.Engine.GroupsManager;
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Engine
{
    public static class EngineExtensions
    {
        public static IServiceCollection AddFantasoccerBackgroundWork(this IServiceCollection services)
        {
            services
                .AddWarmUp(async serviceProvider =>
                {
                    var serieACalendarioJob = serviceProvider.GetRequiredService<SerieACalendarJob>();
                    await serieACalendarioJob.ActionToDoAsync(default);
                });
            services
                .AddBackgroundJob<SerieACalendarJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                })
                .AddBackgroundJob<AllPlayersAndAllTeamsJob>(options =>
                {
                    options.Cron = "0 0 5 * * *";
                })
                .AddBackgroundJob<PlayerOddsJob>(options =>
                {
                    options.RunImmediately = true;
                    options.Cron = "0 0 6,10,12,14,18,20 * * *";
                })
                .AddBackgroundJob<LiveVotesJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                })
                .AddBackgroundJob<LiveJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                })
                .AddBackgroundJob<FinalVotesJob>(options =>
                {
                    options.Cron = "0 0 3,4 * * *";
                })
                .AddBackgroundJob<PlayerImagesJob>(options =>
                {
                    options.Cron = "0 0 1 9 *";
                })
                .AddBackgroundJob<SetFormationJob>(options =>
                {
                    options.Cron = "0 * * * *";
                })
                .AddBackgroundJob<GroupsManagerJob>(options =>
                 {
                     options.Cron = "0 6 * * *";
                 })
                .AddBackgroundJob<NewsJob>(options =>
                {
                    options.Cron = "0 0 12 * * *";
                })
                .AddBackgroundJob<PushNotificationJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                })
                .AddBackgroundJob<HallOfFameJob>(options =>
                {
                    options.Cron = "0 0 3 * * 2";
                })
                .AddBackgroundJob<MarketJob>(options =>
                {
                    options.Cron = "0 0 2 * * *";
                });
            return services;
        }
    }
}
