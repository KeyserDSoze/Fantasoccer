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
            var returnedValue = await _httpClient.GetStringAsync("https://www.gazzetta.it/Calcio/prob_form/").NoContext();
            List<string> games = new();
            var appoggio1 = s_matchFieldContainerRegex.Split(returnedValue);
            for (int i = 1; i < appoggio1.Length; i++)
                games.Add(s_aboveContainerRegex.Split(appoggio1[i])[0]);
            foreach (var app in games)
            {
                var s = app;
                var leul = s_ulRegex.Split(s);
                var playersi1Tit = s_spanRegex.Matches(leul[2]);
                var playersI2Tit = s_spanRegex.Matches(leul[3]);
                var teams = leul[0].Split("https://www.gazzetta.it/calcio/squadre/");
                SetPlayers(playersi1Tit, teams[1].Split('/')[0]);
                SetPlayers(playersI2Tit, teams[2].Split('/')[0]);
                void SetPlayers(MatchCollection values, string realTeam)
                {
                    realTeam = realTeam[..1].ToUpper() + realTeam[1..].ToLower();
                    foreach (Match m in values)
                    {
                        var name = HttpUtility.HtmlDecode(m.ToString().Split('>')[1].Split('<')[0].ToLower());
                        var player = new ChancedRealPlayer
                        {
                            Name = name,
                            RealTeam = new RealTeam { Name = realTeam },
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
