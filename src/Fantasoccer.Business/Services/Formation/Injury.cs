using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Engine.IngestionData
{
    internal class Injury : IFormationParser
    {
        private static readonly Regex TeamsRegex = new("<div id=\"team-");
        private static readonly Regex HeaderRegex = new("<header>");
        private static readonly Regex PlayerNameRegex = new("<strong class=\"item-name\">");
        private static readonly List<string> Urls = new()
        {
            "https://www.fantacalcio.it/indisponibili-serie-a",
            //"https://www.fantacalcio.it/squalificati-e-diffidati-campionato-serie-a",
            //"https://www.fantacalcio.it/infortunati-serie-a"
        };
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync(HttpClient httpClient)
        {
            List<ChancedRealPlayer> players = new();
            foreach (var url in Urls)
            {
                var returnedValue = await httpClient.GetStringAsync(url).NoContext();
                foreach (string teamValue in TeamsRegex.Split(returnedValue).Skip(1).Take(20))
                {
                    try
                    {
                        var headers = HeaderRegex.Split(teamValue).Skip(1).ToList();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            foreach (var possiblePlayer in PlayerNameRegex.Split(headers[i]).Skip(1))
                            {
                                string name = HttpUtility.HtmlDecode(possiblePlayer.Split('<').First().Trim().ToLower());
                                var player = new ChancedRealPlayer
                                {
                                    Name = KeyUtils.GetPlayerKey(name),
                                    Year = FantacalcioTime.ActualYear,
                                    Chance = new Chance()
                                    {
                                        Status = (ChanceType)(i + 1)
                                    }
                                };
                                players.Add(player);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string olaf = ex.Message;
                    }
                }
            }
            return players;
        }
    }
}
