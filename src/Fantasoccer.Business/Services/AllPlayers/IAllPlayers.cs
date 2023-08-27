namespace Fantasoccer.Engine.IngestionData
{
    public interface IAllPlayers
    {
        Task<List<RealPlayer>> GetAsync();
    }
}
