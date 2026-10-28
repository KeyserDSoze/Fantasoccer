using Fantasoccer.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantasoccer.Infrastructure.Caching;

public static class DistributedCacheServiceCollectionExtensions
{
    // One provider instance serves both IDistributedCache and the coordinator, so
    // conditional writes and plain reads always observe the same entries.
    public static IServiceCollection AddAzureBlobDistributedCache(this IServiceCollection services, Action<AzureBlobDistributedCacheOptions> configure)
    {
        services.AddOptions<AzureBlobDistributedCacheOptions>().Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "The Blob distributed cache requires a storage connection string.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName) && options.ContainerName == options.ContainerName.ToLowerInvariant() &&
                options.ContainerName.Length is >= 3 and <= 63, "Cache:ContainerName must be a valid lowercase Blob container name.")
            .Validate(options => IsValidNamespace(options.Namespace), "Cache:Namespace must contain only a-z, 0-9, '.', '_' or '-'.")
            .Validate(options => options.SlidingRefreshRatio is >= 0 and <= 1, "Cache:SlidingRefreshRatio must be between 0 and 1.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AzureBlobDistributedCache>();
        services.AddSingleton<IDistributedCache>(provider => provider.GetRequiredService<AzureBlobDistributedCache>());
        services.AddSingleton<IDistributedCacheCoordinator>(provider => provider.GetRequiredService<AzureBlobDistributedCache>());
        return services;
    }

    public static IServiceCollection AddInMemoryCoordinatedDistributedCache(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InMemoryCoordinatedDistributedCache>();
        services.AddSingleton<IDistributedCache>(provider => provider.GetRequiredService<InMemoryCoordinatedDistributedCache>());
        services.AddSingleton<IDistributedCacheCoordinator>(provider => provider.GetRequiredService<InMemoryCoordinatedDistributedCache>());
        return services;
    }

    private static bool IsValidNamespace(string? value)
    {
        try { CacheBlobNaming.NamespacePrefix(value ?? ""); return true; }
        catch (ArgumentException) { return false; }
    }
}
