using Fantasoccer.Domain.SerieA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static async Task<IServiceCollection> AddStorageInfrastructuresAsync(this IServiceCollection services, IConfiguration configuration)
        {
            var storageConnectionString = configuration["ConnectionStrings:BlobStorage"];
            await services.AddBlobStorage<RealCalendar, int>(storageConnectionString);
            await services.AddBlobStorage<RealPlayersWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<ChancedRealPlayer, YearDayKey>(storageConnectionString);
            await services.AddBlobStorage<Group, string>(storageConnectionString);
            await services.AddBlobStorage<Rank, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<Calendar, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<PlayedGame, GameKey>(storageConnectionString);
            await services.AddRepositoryAsync<SerieATeamsWrapper, int>(async builder =>
            {
                await builder.WithTableStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder
                        .WithPartitionKey(x => x.Year, x => x);
                });
            });
            await services.AddRepositoryAsync<FantacalcioLog, string>(async builder =>
            {
                await builder.WithTableStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder
                        .WithPartitionKey(x => x.PrimaryKey, x => x)
                        .WithRowKey(x => x.SecondaryKey)
                        .WithTimestamp(x => x.Timestamp);
                });
            });
            return services;
        }
        private static Task<IServiceCollection> AddBlobStorage<T, TKey>(this IServiceCollection services, string connectionString)
            where TKey : notnull
        {
            return services.AddRepositoryAsync<T, TKey>(async builder =>
             {
                 await builder.WithBlobStorageAsync(storageBuilder =>
                 {
                     storageBuilder.Settings.ConnectionString = connectionString;
                 });
             });
        }
    }
}
