using Fantasoccer.Business;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddPopulationService();
            services.AddInMemoryInfrastructures();
            services.AddFantasoccerBusiness();
            services.AddSharedBusiness();
        }
    }
}
