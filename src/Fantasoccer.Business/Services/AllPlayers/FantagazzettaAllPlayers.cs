using System.Text.RegularExpressions;
using System.Web;
using RepositoryFramework;

namespace Fantasoccer.Business.IngestionData
{
    internal sealed class FantagazzettaAllPlayers : IAllPlayers
    {
        private static readonly Regex s_playerRowRegex = new("<tr class=\"player-row");
        private static readonly Regex s_playerNameRegex = new("<span>");
        private static readonly Regex s_playerTeamRegex = new("<td class=\"player-team");
        private const string Forward = "<span class=\"role\" data-value=\"a\"></span>";
        private const string Midfielder = "<span class=\"role\" data-value=\"c\"></span>";
        private const string Defensor = "<span class=\"role\" data-value=\"d\"></span>";
        private const string Goalkeeper = "<span class=\"role\" data-value=\"p\"></span>";
        private readonly HttpClient HttpClient;
        private readonly IFootballIngestionReader _context;

        public FantagazzettaAllPlayers(IHttpClientFactory httpClientFactory, IFootballIngestionReader context)
        {
            HttpClient = httpClientFactory.CreateClient("all-players");
            _context = context;
        }

        public async Task<List<RealPlayer>> GetAsync()
        {
            var year = FantacalcioTime.ActualYear;
            var teams = (await _context.ClubsAsync(year))?.Teams ?? [];
            List<RealPlayer> players = new();
            var fantapage = await HttpClient.GetStringAsync("https://www.fantacalcio.it/quotazioni-fantacalcio").NoContext();
            foreach (var playerFromFanta in s_playerRowRegex.Split(fantapage).Skip(1))
            {
                if (playerFromFanta.Contains("out-of-game", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var name = HttpUtility.HtmlDecode(s_playerNameRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim()).Replace("\'", string.Empty);
                var team = HttpUtility.HtmlDecode(s_playerTeamRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim());
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
                    RealTeam = teams.First(x => x.Abbreviation.ToLower() == team.ToLower()),
                    Role = role,
                    IsActive = true,
                    Visible = true
                };
                players.Add(player);
            }
            return players;
        }
    }
}
