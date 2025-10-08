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
                .AddBackgroundJob<SerieAJob>(options =>
                {
                    options.Cron = "0 */5 * * * *";
                })
                .AddBackgroundJob<AllPlayersAndAllTeamsJob>(options =>
                {
                    options.RunImmediately = true;
                    options.Cron = "0 0 5 * * *";
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
                .AddBackgroundJob<PlayerOddsJob>(options =>
                {
                    options.Cron = "0 0 6,10,12,14,18,20 * * *";
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
                //.AddBackgroundJob<NewsJob>(options =>
                //{
                //    options.Cron = "0 0 12 * * *";
                //})
                //.AddBackgroundJob<TeamHelperJob>(options =>
                //{
                //    options.Cron = "0 0 21 * * *";
                //})
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
