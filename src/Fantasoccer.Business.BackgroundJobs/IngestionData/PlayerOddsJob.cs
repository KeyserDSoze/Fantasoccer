using FantacalcioV4.Engine.IngestionData.FormationParser;
using Fantasoccer;
using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class PlayerOddsJob : IBackgroundJob
    {
        private static readonly List<IFormationParser> Parsers = new()
        {
            new Fantagazzetta(),
            new Gazzetta(),
            new Mediaset(),
            new Sky(),
            new Injury()
        };
        private readonly IRepository<FantasoccerLog, string> _logger;
        private readonly HttpClient HttpClient;
        public PlayerOddsJob( IRepository<FantasoccerLog, string> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            HttpClient = httpClientFactory.CreateClient("base");
        }
        public async Task ActionToDoAsync()
        {
            List<Player> players = new();
            foreach (var parser in Parsers)
            {
                var playersFromParser = await parser.GetPlayersAsync(HttpClient).NoContext();
                if (players.Count == 0)
                    players = playersFromParser;
                else
                {
                    foreach (var parsedPlayer in playersFromParser)
                    {
                        Player chancedPlayer = players.FirstOrDefault(x => x.Key == parsedPlayer.Key);
                        if (chancedPlayer == default)
                            players.Add(parsedPlayer);
                        else
                            chancedPlayer.Chance = chancedPlayer.Chance with
                            {
                                Fantagazzetta = chancedPlayer.Chance.Fantagazzetta || parsedPlayer.Chance.Fantagazzetta,
                                Gazzetta = chancedPlayer.Chance.Gazzetta || parsedPlayer.Chance.Gazzetta,
                                Mediaset = chancedPlayer.Chance.Mediaset || parsedPlayer.Chance.Mediaset,
                                Sky = chancedPlayer.Chance.Sky || parsedPlayer.Chance.Sky,
                                Status = chancedPlayer.Chance.Status == ChanceType.Normal ? parsedPlayer.Chance.Status : chancedPlayer.Chance.Status
                            };
                    }
                }
            }
            await new PlayerOddsContext().SaveAsync(players).NoContext();
            await Logger.MessageAsync<PlayerOddsJob>($"Players {players.Count} parsate.").NoContext();
        }

        public Task OnException(Exception exception)
            => Logger.CatchAsync<PlayerOddsJob>(exception);
    }
}