namespace Fantasoccer.Business
{
    public interface ISerieACalendar
    {
        Task<RealCalendar> GetAsync(int min = 1, int max = 38);
    }
}
