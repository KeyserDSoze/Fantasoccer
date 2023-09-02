using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal class Injury : IFormationParser
    {
        private static readonly Regex s_teamsRegex = new("<div id=\"team-");
        private static readonly Regex s_headerRegex = new("<header>");
        private static readonly Regex s_playerNameRegex = new("<strong class=\"item-name\">");
        private static readonly List<string> s_urls = new()
        {
            "https://www.fantacalcio.it/indisponibili-serie-a",
            //"https://www.fantacalcio.it/squalificati-e-diffidati-campionato-serie-a",
            //"https://www.fantacalcio.it/infortunati-serie-a"
        };
        private readonly HttpClient _httpClient;
        public Injury(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            foreach (var url in s_urls)
            {
                var returnedValue = await _httpClient.GetStringAsync(url).NoContext();
                foreach (var teamValue in s_teamsRegex.Split(returnedValue).Skip(1).Take(20))
                {
                    try
                    {
                        var realTeam = teamValue.Split("<span class=\"team-name\">")[1].Split('<')[0];
                        realTeam = realTeam[..1].ToUpper() + realTeam[1..].ToLower();
                        var headers = s_headerRegex.Split(teamValue).Skip(1).ToList();
                        for (var i = 0; i < headers.Count; i++)
                        {
                            foreach (var possiblePlayer in s_playerNameRegex.Split(headers[i]).Skip(1))
                            {
                                var name = HttpUtility.HtmlDecode(possiblePlayer.Split('<').First().Trim().ToLower());
                                var player = new ChancedRealPlayer
                                {
                                    Name = name,
                                    RealTeam = new RealTeam { Name = realTeam },
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
