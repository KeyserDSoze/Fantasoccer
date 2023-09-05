using Fantasoccer.Domain.SerieA;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.Storage.Logger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static async Task<IServiceCollection> AddStorageInfrastructuresAsync(this IServiceCollection services, IConfiguration configuration)
        {
            var storageConnectionString = configuration["ConnectionStrings:Storage"];
            await services.AddBlobStorage<RealCalendar, int>(storageConnectionString);
            await services.AddBlobStorage<RealPlayersWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<ChancedRealPlayerWrapper, YearDayKey>(storageConnectionString);
            await services.AddRepositoryAsync<VotedRealPlayerWrapper, YearDayKey>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.ContainerName = "live";
                }, "live");
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.ContainerName = "official";
                }, "official");
            });
            await services.AddBlobStorage<Group, string>(storageConnectionString);
            await services.AddBlobStorage<Rank, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<Calendar, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<RealTeamWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<StatPlayersWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<PlayedGame, GameKey>(storageConnectionString);
            await services.AddBlobStorage<Team, TeamKey>(storageConnectionString);
            await services.AddBlobStorage<Team, TeamDayKey>(storageConnectionString, "day");
            await services.AddBlobStorage<AppIdentity, string>(storageConnectionString);
            await services.AddBlobStorage<SerieATeamsWrapper, int>(storageConnectionString);
            await services.AddRepositoryAsync<FantasoccerLog, string>(async builder =>
            {
                await builder.WithTableStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder
                        .WithPartitionKey(x => x.PrimaryKey, x => x)
                        .WithRowKey(x => x.SecondaryKey)
                        .WithTimestamp(x => x.Timestamp)
                        .WithTableStorageKeyReader<LoggerKeyReader>();
                });
            });
            await services
                .AddContentRepository()
                .WithBlobStorageIntegrationAsync(builder =>
                {
                    builder.ContainerName = "images";
                    builder.ConnectionString = storageConnectionString;
                    builder.IsPublic = true;
                }, "images");
            return services;
        }
        private static Task<IServiceCollection> AddBlobStorage<T, TKey>(this IServiceCollection services,
            string connectionString,
            string? factoryName = null)
            where TKey : notnull
        {
            return services.AddRepositoryAsync<T, TKey>(async builder =>
             {
                 await builder.WithBlobStorageAsync(storageBuilder =>
                 {
                     storageBuilder.Settings.ConnectionString = connectionString;
                 }, factoryName);
             });
        }
    }
}
