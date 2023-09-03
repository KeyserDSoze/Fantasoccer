using Fantasoccer.Business;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedBusiness(this IServiceCollection services)
        {
            services.AddSingleton<IVoteCalculation, VoteCalculation>();
            services.ScanBusinessForRepositoryFramework();
            return services;
        }
    }
}
