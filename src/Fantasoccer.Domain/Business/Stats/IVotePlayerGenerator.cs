namespace Fantasoccer
{
    public interface IVotePlayerGenerator
    {
        ValueTask<int> GenerateAndSaveAsync(int year, int untilThisDay, int liveDay = 39);

    }
}
