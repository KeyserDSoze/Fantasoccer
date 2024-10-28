namespace Fantasoccer
{
    public interface IVotePlayerGenerator
    {
        ValueTask<int> GenerateAndSaveAsync(int year, int untilThisDay, bool force = false, int withMax = 1);

    }
}
