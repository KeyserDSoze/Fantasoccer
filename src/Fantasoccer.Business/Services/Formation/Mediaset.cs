using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal sealed class Mediaset : IFormationParser
    {
        private const string LineupsUrl = "https://www.sportmediaset.mediaset.it/probabili-formazioni/serie-a/";
        private static readonly Regex s_matchLinkRegex = new("href=\"(?<url>[^\"]*\\?match=[^\"]+)\"", RegexOptions.IgnoreCase);
        private static readonly Regex s_teamNameRegex = new("data-testid=\"team-info-title\"[^>]*>(?<name>[^<]+)<", RegexOptions.IgnoreCase);
        private static readonly Regex s_teamBlockRegex = new("data-testid=\"team-eleven\"", RegexOptions.IgnoreCase);
        private static readonly Regex s_playerNameRegex = new("data-testid=\"player-name\"[^>]*>(?<name>[^<]+)<", RegexOptions.IgnoreCase);
        private readonly HttpClient _httpClient;

        public Mediaset(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }

        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            var index = await _httpClient.GetStringAsync(LineupsUrl).NoContext();
            var matchUrls = s_matchLinkRegex.Matches(index)
                .Select(x => HttpUtility.HtmlDecode(x.Groups["url"].Value))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new Uri(new Uri(LineupsUrl), x).ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<ChancedRealPlayer> players = [];
            foreach (var matchUrl in matchUrls)
            {
                string content;
                try
                {
                    content = await _httpClient.GetStringAsync(matchUrl).NoContext();
                }
                catch (HttpRequestException)
                {
                    continue;
                }

                var teams = s_teamNameRegex.Matches(content)
                    .Select(x => HttpUtility.HtmlDecode(x.Groups["name"].Value).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToList();
                var teamBlocks = s_teamBlockRegex.Split(content).Skip(1).Take(2).ToList();
                if (teams.Count != 2 || teamBlocks.Count != 2)
                    continue;

                for (var teamIndex = 0; teamIndex < 2; teamIndex++)
                {
                    foreach (Match playerMatch in s_playerNameRegex.Matches(teamBlocks[teamIndex]).Take(11))
                    {
                        var name = HttpUtility.HtmlDecode(playerMatch.Groups["name"].Value).Trim();
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        players.Add(new ChancedRealPlayer
                        {
                            Name = name,
                            RealTeam = new RealTeam { Name = teams[teamIndex] },
                            Chance = new Chance { Mediaset = true }
                        });
                    }
                }
            }
            return players;
        }
    }
}
