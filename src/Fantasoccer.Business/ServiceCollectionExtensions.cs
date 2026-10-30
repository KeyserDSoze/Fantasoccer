using Fantasoccer.Business.IngestionData;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    public static class ServiceCollectionExtensions
    {
        // The scoring engines. They read their input only through the Domain contracts (IFootballMatchdayReader, ...).
        public static IServiceCollection AddFantasoccerRuleEngines(this IServiceCollection services)
        {
            services.AddSingleton<ITeamChecker, TeamChecker>();
            services.AddSingleton<IOptimalFormationEngine, OptimalFormationEngine>();
            services.AddSingleton<IRankCalculator, RankCalculator>();
            services.AddSingleton<IRandomCalendar, RandomCalendar>();
            services.AddSingleton<ICardManager, CardManager>();
            services.AddTransient<ITeamCalculator, TeamCalculator>();
            services.AddLeagueCalculationEngines();
            services.AddTransient<IOptimalFormationService, OptimalFormationService>();
            return services;
        }
        private static IServiceCollection AddLeagueCalculationEngines(this IServiceCollection services)
        {
            services.AddFactory<ILeagueCalculator, DefaultLeague>(string.Empty);
            services.AddFactory<ILeagueCalculator, ClassicLeague>(LeagueType.League.ToString());
            services.AddFactory<ILeagueCalculator, ClassicCup>(LeagueType.Cup.ToString());
            services.AddFactory<ILeagueCalculator, ChampionsLeague>(LeagueType.NewCup.ToString());
            services.AddFactory<ILeagueCalculator, SuperLeague>(LeagueType.SuperLeague.ToString());
            return services;
        }

        // The external football sources (Serie A, Fantagazzetta, Gazzetta, official votes, player photos).
        public static IServiceCollection AddFantasoccerSqlDataProviders(this IServiceCollection services)
        {
            services.AddTransient<Services.IPlayerImageRefresher, PlayerImageService>();
            services.AddHttpClient("player-images", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.BaseAddress = new Uri("https://api-sdp.legaseriea.it/v1/serie-a/football/");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddTransient<IOfficialVote, OfficialVote>();
            services.AddTransient<ILiveVote, LiveVote>();
            services.AddTransient<ISerieAInformation, SerieAInformation>();
            services.AddTransient<IAllPlayers, FantagazzettaAllPlayers>();
            services.AddFactory<IFormationParser, Fantagazzetta>(nameof(Fantagazzetta));
            services.AddFactory<IFormationParser, Gazzetta>(nameof(Gazzetta));
            services.AddFactory<IFormationParser, Injury>(nameof(Injury));
            foreach (var name in new[] { "base", "seriea", "all-players", "seriea-gazzetta-calendar" })
                services.AddHttpClient(name, client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    if (name == "seriea-gazzetta-calendar") client.BaseAddress = new Uri("https://api2-mtc.gazzetta.it/api/");
                    else if (name is "seriea" or "all-players") client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Fantasoccer");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9");
                });
            return services;
        }
    }
}
