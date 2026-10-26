using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class LegacyFootballMatchdayReader(IRepository<RealCalendar, int> calendars,
    IRepository<ChancedRealPlayerWrapper, YearDayKey> chances,
    IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> votes) : IFootballMatchdayReader
{
    public async Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances, CancellationToken cancellationToken = default)
    {
        var calendar = await calendars.GetAsync(year, cancellationToken);
        if (day == 0) day = calendar?.NextDay?.SerieADay ?? calendar?.LiveDay?.SerieADay ?? 1;
        var key = new YearDayKey { Year = year, Day = day };
        var live = calendar?.LiveSerieADay == day;
        return new(day, live, calendar?.NextDay?.SerieADay == day,
            calendar?.Days?.FirstOrDefault(x => x.SerieADay == day),
            withVotes && calendar != null && (live || calendar.LastDay?.SerieADay == day) ? await votes.Create("live")!.GetAsync(key, cancellationToken) : null,
            withVotes && calendar != null ? await votes.Create("official")!.GetAsync(key, cancellationToken) : null,
            withChances && calendar != null ? await chances.GetAsync(key, cancellationToken) : null);
    }
}
