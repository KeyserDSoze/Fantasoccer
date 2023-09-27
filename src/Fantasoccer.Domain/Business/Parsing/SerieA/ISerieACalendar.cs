namespace Fantasoccer.Business
{
    public interface ISerieACalendar
    {
        Task<RealCalendar> GetAsync();
        Task<RealDay> GetDayAsync(int day);
    }
}
