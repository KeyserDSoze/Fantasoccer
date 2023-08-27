namespace Fantasoccer.Business.IngestionData
{
    public interface IFormationParser
    {
        Task<List<ChancedRealPlayer>> GetPlayersAsync();
    }
}
