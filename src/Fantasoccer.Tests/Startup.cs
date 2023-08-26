using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddPopulationService();
            services.AddInMemoryInfrastructures();
            services.AddBusiness();
        }
    }
}
