namespace Fantasoccer.Business.IngestionData
{
    public interface IAllPlayers
    {
        Task<List<RealPlayer>> GetAsync();
    }
}
