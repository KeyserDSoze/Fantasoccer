using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Rystem
{
    public static class DependencyInjectionExtensions
    {
        private static event Action AfterRystemIsFullyAdded;
        public static IServiceProvider UseRystem(this IServiceProvider serviceProvider)
        {
            ServiceLocator.ServiceProvider = serviceProvider;
            ServiceLocatorAtRuntime.Rebuild((type) => (serviceProvider ?? ServiceLocator.Providers).GetService(type));
            AfterRystemIsFullyAdded?.Invoke();
            return serviceProvider;
        }
        public static IServiceCollection AddRystemFullyAddedCallback(this IServiceCollection services, Action action)
        {
            AfterRystemIsFullyAdded += action;
            return services;
        }
        public static IServiceCollection AddRystem(this IServiceCollection services)
            => ServiceLocator.Services = services;
    }
}
