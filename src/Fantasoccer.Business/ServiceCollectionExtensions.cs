using Fantasoccer.Business.Authorization;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantasoccer.Business
{
    public static class ServiceCollectionExtensions
    {
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
        public static IServiceCollection AddSharedBusiness(this IServiceCollection services)
        {
            services.TryAddSingleton<IPushNotificationLock, InMemoryPushNotificationLock>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddTransient<PushNotificationSettingsService>();
            services.AddTransient<ITeamCalculator, TeamCalculator>();
            services.AddTransient<IFootballMatchdayReader, LegacyFootballMatchdayReader>();
            services.AddTransient<IFormationStatisticsReader, LegacyFormationStatisticsReader>();
            services.AddTransient<IFootballIngestionReader, LegacyFootballIngestionReader>();
            services.AddSingleton<IOptimalFormationEngine, OptimalFormationEngine>();
            services.AddTransient<IOptimalFormationService, OptimalFormationService>();
            services.AddSingleton<IRankCalculator, RankCalculator>();
            services.AddSingleton<ITeamChecker, TeamChecker>();
            services.AddSingleton<IRandomCalendar, RandomCalendar>();
            services.AddSingleton<IMarketManager, MarketManager>();
            services.AddTransient<IMarketNotificationService, MarketNotificationService>();
            services.AddTransient<IOfficialVote, OfficialVote>();
            services.AddTransient<IVotePlayerGenerator, VotePlayerGenerator>();
            services.AddTransient<ISerieAInformation, SerieAInformation>();
            services.AddTransient<IStatsPlayerGenerator, StatsPlayerGenerator>();
            services.AddTransient<IFantasoccerLogger, FantasoccerLogger>();
            services.AddTransient<PlayerImageService>();
            services.AddHttpClient("base", client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            });
            services.AddHttpClient("player-images", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.BaseAddress = new Uri("https://api-sdp.legaseriea.it/v1/serie-a/football/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddHttpClient("seriea", client =>
            {
                client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.legaseriea.it/it");
            });
            services.AddLeagueCalculationEngines();
            services.ScanBusinessForRepositoryFramework();
            return services;
        }
        public static IServiceCollection AddFantasoccerBusiness(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PushTransportOptions>(configuration.GetSection("Push"));
            services.AddSocialLogin<SocialUserProvider>(x =>
            {
                //x.Google.ClientId = builder.Configuration["SocialLogin:Google:ClientId"];
                //x.Google.ClientSecret = builder.Configuration["SocialLogin:Google:ClientSecret"];
                //x.Google.RedirectDomain = builder.Configuration["SocialLogin:Google:RedirectDomain"];
                x.Microsoft.ClientId = configuration["SocialLogin:Microsoft:ClientId"];
                x.Microsoft.ClientSecret = configuration["SocialLogin:Microsoft:ClientSecret"];
                x.Microsoft.AddUris(configuration["SocialLogin:Microsoft:RedirectDomains"]!.Split(','));
                //x.Facebook.ClientId = builder.Configuration["SocialLogin:Facebook:ClientId"];
                //x.Facebook.ClientSecret = builder.Configuration["SocialLogin:Facebook:ClientSecret"];
                //x.Facebook.RedirectDomain = builder.Configuration["SocialLogin:Facebook:RedirectDomain"];
            },
            x =>
            {
                x.BearerTokenExpiration = TimeSpan.FromHours(24);
                x.RefreshTokenExpiration = TimeSpan.FromDays(100);
            });
            services.AddSingleton<AuctionHubService>();
            services.AddSingleton<EncryptionService>();
            services.AddFactory<IFormationParser, Fantagazzetta>(nameof(Fantagazzetta));
            services.AddFactory<IFormationParser, Gazzetta>(nameof(Gazzetta));
            services.AddFactory<IFormationParser, Injury>(nameof(Injury));
            services.AddFactory<IFormationParser, Mediaset>(nameof(Mediaset));
            services.AddFactory<IFormationParser, Sky>(nameof(Sky));
            services.AddTransient<IAllPlayers, FantagazzettaAllPlayers>();
            services.AddSingleton<FantacalcioPlayerDescriptionService>();
            services.AddTransient<ILiveVote, LiveVote>();
            //services.AddTransient<ILiveVote, LiveVoteWithHtml>();
            services.AddTransient<IPushNotificationService, PushNotificationService>();
            services.AddHttpClient("web-push", client => client.Timeout = TimeSpan.FromSeconds(20))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddHttpClient("base", client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            });
            services.AddHttpClient("all-players", client =>
            {
                client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.fantacalcio.it/quotazioni-fantacalcio");
            });
            services.AddHttpClient("seriea", client =>
            {
                client.BaseAddress = new Uri("https://www.legaseriea.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.legaseriea.it/it");
            });
            services.AddHttpClient("seriea-gazzetta-calendar", client =>
            {
                client.BaseAddress = new Uri("https://api2-mtc.gazzetta.it/api/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.gazzetta.it/calcio/serie-a/calendario-risultati/");
            });
            services.ScanAuthorizationForRepositoryFramework();
            return services;
        }

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
