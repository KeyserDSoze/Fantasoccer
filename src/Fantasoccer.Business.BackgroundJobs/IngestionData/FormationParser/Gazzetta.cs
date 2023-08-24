using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace FantacalcioV4.Engine.IngestionData.FormationParser
{
    internal class Gazzetta : IFormationParser
    {
        private static readonly Regex MatchFieldContainerRegex = new("class=\"matchFieldContainer");
        private static readonly Regex AboveContainerRegex = new("class=\"go-above-container");
        private static readonly Regex UlRegex = new("<ul");
        private static readonly Regex SpanRegex = new("<span class=\"team-player\">[^<]*</span>");
        public async Task<List<Player>> GetPlayersAsync(HttpClient httpClient)
        {
            List<Player> players = new();
            string returnedValue = await httpClient.GetStringAsync("https://www.gazzetta.it/Calcio/prob_form/").NoContext();
            List<string> games = new();
            string[] appoggio1 = MatchFieldContainerRegex.Split(returnedValue);
            for (int i = 1; i < appoggio1.Length; i++)
                games.Add(AboveContainerRegex.Split(appoggio1[i])[0]);
            foreach (string app in games)
            {
                string s = app;
                string[] leul = UlRegex.Split(s);
                MatchCollection playersi1Tit = SpanRegex.Matches(leul[2]);
                MatchCollection playersI2Tit = SpanRegex.Matches(leul[3]);
                SetPlayers(playersi1Tit);
                SetPlayers(playersI2Tit);
                void SetPlayers(MatchCollection values)
                {
                    foreach (Match m in values)
                    {
                        string name = HttpUtility.HtmlDecode(m.ToString().Split('>')[1].Split('<')[0].ToLower());
                        Player player = new(FantacalcioId.GetPlayerKey(FantacalcioTime.ActualYear, name), 0, default)
                        {
                            Chance = new Chance(false, true, false, false, ChanceType.Normal)
                        };
                        players.Add(player);
                    }
                }
            }
            return players;
        }
    }
}
