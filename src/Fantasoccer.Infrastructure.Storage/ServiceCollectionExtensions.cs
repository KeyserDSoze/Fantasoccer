using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static async Task<IServiceCollection> AddStorageInfrastructuresAsync(this IServiceCollection services, IConfiguration configuration)
        {
            var storageConnectionString = configuration["ConnectionStrings:BlobStorage"];
            await services.AddRepositoryAsync<RealCalendar, int>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                });
            });
            await services.AddRepositoryAsync<RealPlayersWrapper, int>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                });
            });
            await services.AddRepositoryAsync<VotePlayersWrapper, int>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                });
            });
            await services.AddRepositoryAsync<Group, string>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
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
    }
}
