namespace Fantasoccer.Engine.IngestionData
{
    public interface IFormationParser
    {
        Task<List<ChancedRealPlayer>> GetPlayersAsync(HttpClient httpClient);
    }
}
