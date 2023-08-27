using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal class Gazzetta : IFormationParser
    {
        private static readonly Regex s_matchFieldContainerRegex = new("class=\"matchFieldContainer");
        private static readonly Regex s_aboveContainerRegex = new("class=\"go-above-container");
        private static readonly Regex s_ulRegex = new("<ul");
        private static readonly Regex s_spanRegex = new("<span class=\"team-player\">[^<]*</span>");
        private readonly HttpClient _httpClient;
        public Gazzetta(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            string returnedValue = await _httpClient.GetStringAsync("https://www.gazzetta.it/Calcio/prob_form/").NoContext();
            List<string> games = new();
            string[] appoggio1 = s_matchFieldContainerRegex.Split(returnedValue);
            for (int i = 1; i < appoggio1.Length; i++)
                games.Add(s_aboveContainerRegex.Split(appoggio1[i])[0]);
            foreach (string app in games)
            {
                string s = app;
                string[] leul = s_ulRegex.Split(s);
                MatchCollection playersi1Tit = s_spanRegex.Matches(leul[2]);
                MatchCollection playersI2Tit = s_spanRegex.Matches(leul[3]);
                SetPlayers(playersi1Tit);
                SetPlayers(playersI2Tit);
                void SetPlayers(MatchCollection values)
                {
                    foreach (Match m in values)
                    {
                        var name = HttpUtility.HtmlDecode(m.ToString().Split('>')[1].Split('<')[0].ToLower());
                        var player = new ChancedRealPlayer
                        {
                            Name = name,
                            Year = FantacalcioTime.ActualYear,
                            Chance = new Chance()
                            {
                                Gazzetta = true
                            }
                        };
                        players.Add(player);
                    }
                }
            }
            return players;
        }
    }
}
