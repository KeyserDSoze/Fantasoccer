using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal class Gazzetta : IFormationParser
    {
        private static readonly Regex s_matchFieldContainerRegex = new("class=\"bck-box-match-details");
        //private static readonly Regex s_matchFieldContainerRegex = new("<div id=\"match-");
        private static readonly Regex s_aboveContainerRegex = new("class=\"go-above-container");
        private static readonly Regex s_ulRegex = new("<ul");
        private static readonly Regex s_spanRegex = new("<span class=\"lineup-team__name\">[^<]*</span>");
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
            var gamesAsHtml = s_matchFieldContainerRegex.Split(returnedValue);
            for (var i = 1; i < gamesAsHtml.Length; i++)
                games.Add(s_aboveContainerRegex.Split(gamesAsHtml[i])[0]);
            foreach (var app in games)
            {
                var s = app;
                var theListOfPlayersAsHtml = s_ulRegex.Split(s);
                if (theListOfPlayersAsHtml.Length < 3)
                    continue;

                var listOfPlayersOfHomeTeam = s_spanRegex.Matches(theListOfPlayersAsHtml[1]);
                var listOfPlayersOfAwayTeam = s_spanRegex.Matches(theListOfPlayersAsHtml[2]);
                var teams = theListOfPlayersAsHtml[0].Split("https://www.gazzetta.it/calcio/squadre/");
                if (teams.Length < 3)
                    continue;

                SetPlayers(listOfPlayersOfHomeTeam, teams[1].Split('/')[0]);
                SetPlayers(listOfPlayersOfAwayTeam, teams[2].Split('/')[0]);
                void SetPlayers(MatchCollection values, string realTeam)
                {
                    if (string.IsNullOrWhiteSpace(realTeam))
                        return;

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
