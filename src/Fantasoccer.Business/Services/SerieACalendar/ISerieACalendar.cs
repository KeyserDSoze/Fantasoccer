namespace Fantasoccer.Business
{
    public interface ISerieACalendar
    {
        Task<RealCalendar> GetAsync();
    }
}
