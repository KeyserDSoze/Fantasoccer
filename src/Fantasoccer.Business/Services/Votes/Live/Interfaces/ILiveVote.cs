namespace Fantasoccer.Business.IngestionData
{
    public interface ILiveVote
    {
        Task<List<VotedRealPlayer>> GetAsync();
        int Day { get; }
    }
}
