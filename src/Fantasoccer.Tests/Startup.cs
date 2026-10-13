using Fantasoccer.Business;
using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Engine.IngestionData;
using Fantasoccer.InMemory;
using Fantasoccer.Domain;
using Fantasoccer.Tests.Tests.BackgroundJobs;
using Fantasoccer.Tests.Tests.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;
using System.Threading.Concurrent;

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
            services.AddRepository<PushNotificationSettings, string>(builder => builder.WithInMemory());
             services.AddRepository<AuctionWrapper, string>(builder => builder.WithInMemory());
             services.AddRepository<OpeningCompetitionStandings, OpeningCompetitionKey>(builder => builder.WithInMemory());
            services.AddFantasoccerBusiness(configuration);
            services.AddSharedBusiness();
            services.AddLock();
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
