namespace Fantasoccer.Business
{
    public interface ISerieAInformation
    {
        Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38);
    }
}
