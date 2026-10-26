using Fantasoccer.Domain;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class StatsPlayerGenerator(IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
    IRepository<StatPlayersWrapper, int> statistics, IRepository<RealPlayersWrapper, int> players, ITeamCalculator scoring) : IStatsPlayerGenerator
{
    public async ValueTask GenerateAndSaveAsync(int year, int untilThisDay)
    {
        var catalogue = await players.GetAsync(year);
        var votes = new Dictionary<int, VotedRealPlayerWrapper>();
        for (var day = untilThisDay; day > 0; day--)
            if (await factory.Create("official")!.GetAsync(new() { Year = year, Day = day }) is { } rows) votes[day] = rows;
        var result = FootballStatisticsCalculator.Calculate(catalogue?.Players ?? [], untilThisDay, votes, scoring);
        if (await statistics.ExistAsync(year)) await statistics.UpdateAsync(year, result);
        else await statistics.InsertAsync(year, result);
    }
}
