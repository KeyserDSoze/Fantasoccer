using Fantasoccer.Business;
using Fantasoccer.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            IConfiguration configuration = new ConfigurationBuilder().Build();
            services.AddPopulationService();
            services.AddInMemoryInfrastructures();
            services.AddFantasoccerBusiness(configuration);
            services.AddSharedBusiness();
        }
    }
}
