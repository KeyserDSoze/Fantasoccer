using Fantasoccer.Business.BackgroundJobs.GroupsManager;
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
                    await serieACalendarioJob.ActionToDoAsync();
                    var allPlayersAndAllTeamsJob = serviceProvider.GetRequiredService<AllPlayersAndAllTeamsJob>();
                    await allPlayersAndAllTeamsJob.ActionToDoAsync();
                });
            services
                .AddBackgroundJob<SerieACalendarJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<AllPlayersAndAllTeamsJob>(options =>
                {
                    options.Cron = "0 0 5 * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<PlayerOddsJob>(options =>
                {
                    options.Cron = "0 0 6,10,12,14,18,20 * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<LiveVotesJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<LiveJob>(options =>
                {
                    options.Cron = "0 */1 * * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<FinalVotesJob>(options =>
                {
                    options.Cron = "0 45 2,5 * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<PlayerImagesJob>(options =>
                {
                    options.Cron = "0 0 1 9 *";
                })
                .AddBackgroundJob<SetFormationJob>(options =>
                {
                    options.Cron = "0 * * * *";
                    options.RunImmediately = true;
                })
                .AddBackgroundJob<GroupsManagerJob>(options =>
                 {
                     options.Cron = "6 * * * *";
                     options.RunImmediately = true;
                 });
            return services;
        }
    }
}
