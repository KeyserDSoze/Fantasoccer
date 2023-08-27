namespace Fantasoccer.Business.IngestionData
{
    public interface IOfficialVote
    {
        Task<List<VotedRealPlayer>> GetAsync(int year, int serieADay);
    }
}
