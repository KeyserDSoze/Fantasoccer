using Fantasoccer.Business;
using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.InMemory;
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
        }
    }
}
