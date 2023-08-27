using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Engine.IngestionData
{
    internal sealed class FantagazzettaAllPlayers : IAllPlayers
    {
        private static readonly Regex PlayerRowRegex = new("<tr class=\"player-row");
        private static readonly Regex PlayerNameRegex = new("<a class=\"player-name");
        private static readonly Regex PlayerTeamRegex = new("<td class=\"player-team");
        private const string Forward = "<span class=\"role\" data-value=\"a\"></span>";
        private const string Midfielder = "<span class=\"role\" data-value=\"c\"></span>";
        private const string Defensor = "<span class=\"role\" data-value=\"d\"></span>";
        private const string Goalkeeper = "<span class=\"role\" data-value=\"p\"></span>";
        private readonly HttpClient HttpClient;
        public FantagazzettaAllPlayers(IHttpClientFactory httpClientFactory)
        {
            HttpClient = httpClientFactory.CreateClient("base");
        }

        public async Task<List<RealPlayer>> GetAsync()
        {
            List<RealPlayer> players = new();
            var fantapage = await HttpClient.GetStringAsync("https://www.fantacalcio.it/quotazioni-fantacalcio").NoContext();
            var year = FantacalcioTime.ActualYear;
            foreach (var playerFromFanta in PlayerRowRegex.Split(fantapage).Skip(1))
            {
                var name = HttpUtility.HtmlDecode(PlayerNameRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim()).Replace("\'", string.Empty);
                var team = HttpUtility.HtmlDecode(PlayerTeamRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim());
                var role = Role.Undefined;
                if (playerFromFanta.Contains(Forward))
                    role = Role.Forward;
                else if (playerFromFanta.Contains(Midfielder))
                    role = Role.Midfielder;
                else if (playerFromFanta.Contains(Defensor))
                    role = Role.Defensor;
                else if (playerFromFanta.Contains(Goalkeeper))
                    role = Role.GoalKeeper;
                var player = new RealPlayer
                {
                    Name = name,
                    RealTeam = team.ToLower(),
                    Role = role,
                    IsActive = true,
                    Year = year
                };
                players.Add(player);
            }
            return players;
        }
    }
}
