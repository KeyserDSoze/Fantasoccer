using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal class Fantagazzetta : IFormationParser
    {
        private static readonly Regex s_playerListRegex = new("<ul class=\"player-list starters\">");
        private static readonly Regex s_playerItemRegex = new("<li class=\"player-item");
        private static readonly Regex s_spanRegex = new(@"<span>[^<]*</span>");
        private readonly HttpClient _httpClient;
        public Fantagazzetta(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            var returnedValue = await _httpClient.GetStringAsync("https://www.fantacalcio.it/probabili-formazioni-serie-a").NoContext();
            var matches = s_playerListRegex.Split(returnedValue);
            foreach (var match in matches.Skip(1))
            {
                foreach (var player in s_playerItemRegex.Split(match).Skip(1).Take(11))
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;
                    var name = s_spanRegex.IsMatch(player) ? HttpUtility.HtmlDecode(s_spanRegex.Match(player).Value.Split('>')[1].Split('<')[0]) : string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    players.Add(new()
                    {
                        Name = name,
                        Year = FantacalcioTime.ActualYear,
                        Chance = new Chance()
                        {
                            Fantagazzetta = true
                        }
                    });
                }
            }
            return players;
        }
    }
}
