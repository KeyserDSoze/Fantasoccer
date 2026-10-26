using RepositoryFramework;

namespace Fantasoccer.Business;

internal sealed class LegacyFootballIngestionReader(IRepository<RealCalendar, int> calendar,
    IRepository<RealPlayersWrapper, int> players, IRepository<RealTeamWrapper, int> clubs) : IFootballIngestionReader
{
    public async Task<RealCalendar?> CalendarAsync(int year) => await calendar.GetAsync(year);
    public async Task<RealPlayersWrapper?> PlayersAsync(int year) => await players.GetAsync(year);
    public async Task<RealTeamWrapper?> ClubsAsync(int year) => await clubs.GetAsync(year);
}
