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
            services.AddSingleton<IRandomCalendar, RandomCalendar>();
            services.AddFactory<ILeagueCalculator, DefaultLeague>(string.Empty);
            services.AddFactory<ILeagueCalculator, ClassicLeague>(LeagueType.League.ToString());
            services.AddFactory<ILeagueCalculator, ClassicCup>(LeagueType.Cup.ToString());
            services.AddFactory<ILeagueCalculator, ChampionsLeague>(LeagueType.NewCup.ToString());
            services.AddFactory<ILeagueCalculator, SuperLeague>(LeagueType.SuperLeague.ToString());
            services.ScanBusinessForRepositoryFramework();
            return services;
        }
    }
}
