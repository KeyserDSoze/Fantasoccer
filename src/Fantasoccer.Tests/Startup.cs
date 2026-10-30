using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests;

// Engine/parser tests use the same registrations as production. No storage backend or retired repositories.
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddPopulationService();
        services.AddFantasoccerRuleEngines();
        services.AddFantasoccerSqlDataProviders();
        services.AddSingleton<IFootballMatchdayReader, EmptyMatchdayReader>();
    }

    private sealed class EmptyMatchdayReader : IFootballMatchdayReader
    {
        public Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FootballMatchdayInput(day, false, false, null, null, null, null));
    }
}
