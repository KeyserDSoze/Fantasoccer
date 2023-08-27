using System.Text.RegularExpressions;

namespace Fantasoccer.Business.IngestionData
{
    internal class Sky : IFormationParser
    {
        private static readonly Regex s_playersRegex = new(@"<span[\s\S]*?<\/span>");
        private readonly HttpClient _httpClient;
        public Sky(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            var returnedValue = await _httpClient.GetStringAsync("https://sport.sky.it/calcio/serie-a/probabili-formazioni/").NoContext();
            var values = s_playersRegex.Split(returnedValue);
            foreach (var player in values)
            {
                var name = s_playersRegex.Match(player).Value.Split('>')[1].Split('<')[0];
                var chancedPlayer = new ChancedRealPlayer
                {
                    Name = name,
                    Year = FantacalcioTime.ActualYear,
                    Chance = new Chance()
                    {
                        Sky = true
                    }
                };
                players.Add(chancedPlayer);
            }
            return players;
        }
    }
}
