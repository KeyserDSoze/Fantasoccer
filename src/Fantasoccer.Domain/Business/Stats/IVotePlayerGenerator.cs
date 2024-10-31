namespace Fantasoccer
{
    public interface IVotePlayerGenerator
    {
        ValueTask<bool> GenerateAndSaveAsync(int year, int untilThisDay, bool force = false, int withMax = 1);

    }
}
