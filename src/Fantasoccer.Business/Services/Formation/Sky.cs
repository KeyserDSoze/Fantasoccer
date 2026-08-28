using System.Text.Json;
using System.Web;

namespace Fantasoccer.Business.IngestionData
{
    internal sealed class Sky : IFormationParser
    {
        private const string PropsMarker = "<script type=\"application/json\" data-props=\"true\">";
        private readonly HttpClient _httpClient;

        public Sky(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }

        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            var content = await _httpClient.GetStringAsync("https://sport.sky.it/football/predicted-lineup-homepage/21/widget.html").NoContext();
            var start = content.IndexOf(PropsMarker, StringComparison.Ordinal);
            if (start < 0)
                return [];

            start += PropsMarker.Length;
            var end = content.IndexOf("</script>", start, StringComparison.Ordinal);
            if (end < 0)
                return [];

            Root? root;
            try
            {
                root = JsonSerializer.Deserialize<Root>(HttpUtility.HtmlDecode(content[start..end]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return [];
            }

            List<ChancedRealPlayer> players = [];
            foreach (var match in root?.MatchList ?? [])
            {
                AddPlayers(match.Home);
                AddPlayers(match.Away);
            }
            return players;

            void AddPlayers(Team? team)
            {
                if (string.IsNullOrWhiteSpace(team?.Name))
                    return;

                foreach (var player in team.PlayerList?.StartingLineup ?? [])
                {
                    var name = $"{player.Name} {player.Surname}".Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        name = player.FullName?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    players.Add(new ChancedRealPlayer
                    {
                        Name = name,
                        RealTeam = new RealTeam { Name = team.Name },
                        Chance = new Chance { Sky = true }
                    });
                }
            }
        }

        private sealed class Root
        {
            public List<Match>? MatchList { get; set; }
        }

        private sealed class Match
        {
            public Team? Home { get; set; }
            public Team? Away { get; set; }
        }

        private sealed class Team
        {
            public string? Name { get; set; }
            public PlayerList? PlayerList { get; set; }
        }

        private sealed class PlayerList
        {
            public List<StartingPlayer>? StartingLineup { get; set; }
        }

        private sealed class StartingPlayer
        {
            public string? Name { get; set; }
            public string? Surname { get; set; }
            public string? FullName { get; set; }
        }
    }
}
