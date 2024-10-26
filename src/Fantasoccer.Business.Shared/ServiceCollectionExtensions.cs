using Fantasoccer;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;

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
            services.AddTransient<IOfficialVote, OfficialVote>();
            services.AddTransient<IVotePlayerGenerator, VotePlayerGenerator>();
            services.AddTransient<ISerieACalendar, SerieACalendar>();
            services.AddTransient<IStatsPlayerGenerator, StatsPlayerGenerator>();
            services.AddHttpClient("base", client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            });
            services.AddHttpClient("seriea", client =>
            {
                client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.legaseriea.it/it");
            });
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
