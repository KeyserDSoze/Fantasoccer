namespace Fantasoccer
{
    public interface IStatsPlayerGenerator
    {
        ValueTask GenerateAndSaveAsync(int year, int untilThisDay);
    }
}
