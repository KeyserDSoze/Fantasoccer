using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;
using Fantasoccer.InMemory;

namespace Fantasoccer.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddPopulationService();
            services.AddInMemoryInfrastructures();
            services.AddFantasoccerBusiness();
        }
    }
}
