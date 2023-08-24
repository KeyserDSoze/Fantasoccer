using Fantasoccer;

namespace FantacalcioV4.Engine.IngestionData.FormationParser
{
    internal interface IFormationParser
    {
        Task<List<Player>> GetPlayersAsync(HttpClient httpClient);
    }
}
