using Fantasoccer;
using System.Text.RegularExpressions;
using System.Timers;
using System.Web;

namespace FantacalcioV4.Engine.IngestionData
{
    public class AllPlayersJob : IBackgroundJob
    {
        private static readonly Regex PlayerRowRegex = new("<tr class=\"player-row");
        private static readonly Regex PlayerNameRegex = new("<a class=\"player-name");
        private static readonly Regex PlayerTeamRegex = new("<td class=\"player-team");
        private const string Forward = "<span class=\"role\" data-value=\"a\"></span>";
        private const string Midfielder = "<span class=\"role\" data-value=\"c\"></span>";
        private const string Defensor = "<span class=\"role\" data-value=\"d\"></span>";
        private const string Goalkeeper = "<span class=\"role\" data-value=\"p\"></span>";
        private readonly IRepository<FantacalcioLog, string> _logger;
        private readonly HttpClient HttpClient;
        public AllPlayersJob( IRepository<FantacalcioLog, string> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            HttpClient = httpClientFactory.CreateClient("base");
        }

        public async Task ActionToDoAsync()
        {
            List<RealPlayer> players = new();
            string fantapage = await HttpClient.GetStringAsync("https://www.fantacalcio.it/quotazioni-fantacalcio").NoContext();

            foreach (var playerFromFanta in PlayerRowRegex.Split(fantapage).Skip(1))
            {
                string name = HttpUtility.HtmlDecode(PlayerNameRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim()).Replace("\'", string.Empty);
                string team = HttpUtility.HtmlDecode(PlayerTeamRegex.Split(playerFromFanta)[1].Split('<').First().Split('>').Last().Trim());
                Role role = Role.Undefined;
                if (playerFromFanta.Contains(Forward))
                    role = Role.Forward;
                else if (playerFromFanta.Contains(Midfielder))
                    role = Role.Midfielder;
                else if (playerFromFanta.Contains(Defensor))
                    role = Role.Defensor;
                else if (playerFromFanta.Contains(Goalkeeper))
                    role = Role.GoalKeeper;
                RealPlayer player = new(name, team.ToLower(), role, FantacalcioTime.ActualYear);
                player.IsActive = true;
                players.Add(player);
            }

            var oldPlayers = await new AllPlayerContext(FantacalcioTime.ActualYear).GetAsync().NoContext() ?? new List<RealPlayer>();
            foreach (var oldPlayer in oldPlayers)
            {
                if (!players.Any(x => x.Key == oldPlayer.Key))
                {
                    oldPlayer.IsActive = false;
                    players.Add(oldPlayer);
                }
            }
            await new AllPlayerContext(FantacalcioTime.ActualYear).SaveAsync(players).NoContext();
            await new AllPlayerKey { Year = FantacalcioTime.ActualYear }.RestoreAsync().NoContext();
            await Logger.MessageAsync<AllPlayersJob>($"Players count: {players.Count} for year {FantacalcioTime.ActualYear}").NoContext();
        }

        public Task OnException(Exception exception)
            => Logger.CatchAsync<AllPlayersJob>(exception);
    }
}