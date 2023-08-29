namespace Fantasoccer.Business.IngestionData
{
    public interface ILiveVote
    {
        Task<List<VotedRealPlayer>> GetAsync(int year, int day);
    }
}
