namespace Fantasoccer.Business;

// Context required by the provider parsers, not a CRUD/repository abstraction.
public interface IFootballIngestionReader
{
    Task<RealCalendar?> CalendarAsync(int year);
    Task<RealPlayersWrapper?> PlayersAsync(int year);
    Task<RealTeamWrapper?> ClubsAsync(int year);
}
