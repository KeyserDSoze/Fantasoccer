using System.Text.RegularExpressions;
using System.Timers;
using System.Web;
using Fantasoccer;
using RepositoryFramework;

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
        private readonly IRepository<RealPlayersWrapper, int> _realPlayersRepository;
        private readonly HttpClient HttpClient;
        public AllPlayersJob(IRepository<FantacalcioLog, string> logger,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _realPlayersRepository = realPlayersRepository;
            HttpClient = httpClientFactory.CreateClient("base");
        }

        public async Task ActionToDoAsync()
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

            var oldPlayers = await _realPlayersRepository.GetAsync(year).NoContext() ?? new RealPlayersWrapper { Players = new List<RealPlayer>() };
            foreach (var oldPlayer in oldPlayers.Players)
            {
                var oldPlayerKey = KeyUtils.GetPlayerKey(oldPlayer.Name);
                if (!players.Any(x => KeyUtils.GetPlayerKey(x.Name) == oldPlayerKey))
                {
                    players.Add(oldPlayer);
                }
                else
                {
                    oldPlayer.IsActive = false;
                }
            }
            await _realPlayersRepository.UpdateAsync(year, oldPlayers).NoContext();
            await _logger.InsertAsync(Guid.NewGuid().ToString(), new FantacalcioLog { Message = $"Players count: {players.Count} for year {FantacalcioTime.ActualYear}" }).NoContext();
        }

        public Task OnException(Exception exception)
            => Logger.CatchAsync<AllPlayersJob>(exception);
    }
}
