using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBusiness(this IServiceCollection services)
        {
            services.AddSingleton<RandomCalendar>();
            return services;
        }
    }
}
