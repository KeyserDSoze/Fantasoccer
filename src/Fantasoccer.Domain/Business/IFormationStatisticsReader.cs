using Fantasoccer.Domain;

namespace Fantasoccer.Business;

public sealed record FormationStatistics(IReadOnlyList<StatPlayer> Players, IReadOnlyList<RealRankedTeam> Clubs);
public interface IFormationStatisticsReader
{
    Task<FormationStatistics> ReadAsync(int year, CancellationToken cancellationToken = default);
}
