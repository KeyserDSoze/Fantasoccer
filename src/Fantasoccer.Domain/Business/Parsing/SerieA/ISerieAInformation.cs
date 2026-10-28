namespace Fantasoccer.Business
{
    public interface ISerieAInformation
    {
        Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38);
        // The current public provider has no verified historical-season route.
        // An adapter supporting it may override this method; never relabel the
        // current calendar as an older season during a recovery.
        Task<RealCalendar> GetCalendarForSeasonAsync(int year, int min = 1, int max = 38)
            => year == FantacalcioTime.ActualYear ? GetCalendarAsync(min, max)
                : throw new NotSupportedException("The calendar provider cannot verify this historical season.");
    }
}
