using Fantasoccer.Business;
using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Fantasoccer.InMemory;
using Fantasoccer.Tests.Tests.BackgroundJobs;
using Fantasoccer.Tests.Tests.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SocialLogin:Microsoft:ClientId"] = "test-client-id",
                    ["SocialLogin:Microsoft:ClientSecret"] = "test-client-secret",
                    ["SocialLogin:Microsoft:RedirectDomains"] = "https://localhost"
                })
                .Build();
            services.AddPopulationService();
            services.AddInMemoryInfrastructures();
            services.AddFantasoccerBusiness(configuration);
            services.AddSharedBusiness();
            services.AddTransient<MarketJob>();
            services.AddSingleton<TestOfficialVote>();
            services.AddSingleton<IOfficialVote>(x => x.GetRequiredService<TestOfficialVote>());
            services.AddSingleton<TestAllPlayers>();
            services.AddSingleton<TestStatsPlayerGenerator>();
            services.AddSingleton<IStatsPlayerGenerator>(x => x.GetRequiredService<TestStatsPlayerGenerator>());
            services.AddSingleton<AllPlayersAndAllTeamsJob>(x =>
                ActivatorUtilities.CreateInstance<AllPlayersAndAllTeamsJob>(
                    x,
                    x.GetRequiredService<TestAllPlayers>()));
            services.AddSingleton<TestLiveVote>();
            services.AddSingleton<ILiveVote>(x => x.GetRequiredService<TestLiveVote>());
            services.AddSingleton<LiveVotesJob>();
            services.AddSingleton<TestPlayerImagesHandler>();
            services.AddHttpClient("player-images")
                .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestPlayerImagesHandler>());
            services.AddSingleton<PlayerImagesJob>();
            services.AddTransient<PlayerOddsJob>();
            services.AddSingleton<TestAllPlayersHandler>();
            services.AddHttpClient("all-players")
                .ConfigurePrimaryHttpMessageHandler(x => x.GetRequiredService<TestAllPlayersHandler>());
        }
    }
}
