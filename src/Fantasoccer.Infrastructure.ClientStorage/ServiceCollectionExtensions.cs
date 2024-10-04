using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static async Task<IServiceCollection> AddStorageForClientInfrastructuresAsync(this IServiceCollection services, string storageConnectionString)
        {
            await services
                .AddContentRepository()
                .WithBlobStorageIntegrationAsync(builder =>
                {
                    builder.ContainerName = "logos";
                    builder.ConnectionString = storageConnectionString;
                    builder.IsPublic = true;
                }, "logos");
            return services;
        }
    }
}
