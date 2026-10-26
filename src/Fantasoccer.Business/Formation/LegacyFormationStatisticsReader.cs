using Fantasoccer.Domain;
using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class LegacyFormationStatisticsReader(IRepository<StatPlayersWrapper, int> statistics,
    IRepository<RealRank, int> ranks) : IFormationStatisticsReader
{
    public async Task<FormationStatistics> ReadAsync(int year, CancellationToken cancellationToken = default) =>
        new((await statistics.GetAsync(year, cancellationToken))?.Players ?? [], (await ranks.GetAsync(year, cancellationToken))?.Teams ?? []);
}
