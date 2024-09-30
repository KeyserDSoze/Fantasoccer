using Fantasoccer;
using Fantasoccer.Business;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedBusiness(this IServiceCollection services)
        {
            services.AddTransient<ITeamCalculator, TeamCalculator>();
            services.AddSingleton<IRankCalculator, RankCalculator>();
            services.AddSingleton<ITeamChecker, TeamChecker>();
            services.AddFactory<ILeagueCalculator, ClassicLeague>(LeagueType.League.ToString());
            services.AddFactory<ILeagueCalculator, ClassicCup>(LeagueType.Cup.ToString());
            services.ScanBusinessForRepositoryFramework();
            return services;
        }
    }
}
