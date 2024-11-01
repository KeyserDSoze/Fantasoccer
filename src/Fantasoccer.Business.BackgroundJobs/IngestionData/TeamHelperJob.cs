using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Timers;
using Azure.AI.OpenAI;
using Fantasoccer.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager
{
    internal class TeamHelperJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Team, TeamKey> _teamRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IRepository<TeamWithAi, TeamDayKey> _teamWithAiRepository;
        private readonly IRepository<RealRank, int> _realRankRepository;
        private readonly IConfiguration _configuration;
        private readonly ILeagueCalculator _superLeagueCalculator;

        public TeamHelperJob(IFantasoccerLogger logger,
            ITeamCalculator teamCalculator,
            IFactory<ILeagueCalculator> leagueCalculator,
            IRepository<Group, string> groups,
            IRepository<RealCalendar, int> serieACalendar,
            IConfiguration configuration,
            IRepository<Team, TeamKey> teamRepository,
            IRepository<RealRank, int> realRankRepository,
            IRepository<TeamWithAi, TeamDayKey> teamWithAiRepository)
        {
            _logger = logger;
            _teamCalculator = teamCalculator;
            _groups = groups;
            _serieACalendar = serieACalendar;
            _configuration = configuration;
            _teamRepository = teamRepository;
            _realRankRepository = realRankRepository;
            _teamWithAiRepository = teamWithAiRepository;
            _superLeagueCalculator = leagueCalculator.Create(LeagueType.SuperLeague.ToString())!;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var calendar = await _serieACalendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            var nextDay = (calendar?.LiveDay?.SerieADay ?? calendar?.LastDay?.SerieADay ?? 38) + 1;
            if (nextDay < 38)
            {
                var currentDay = calendar!.Days.First(x => x.SerieADay == nextDay);
                var groups = await _groups.ToListAsEntityAsync().NoContext();
                var serieARank = await _realRankRepository.GetAsync(FantacalcioTime.ActualYear).NoContext();
                foreach (var group in groups.Where(x => x.Baskets != null))
                {
                    foreach (var basket in group.Baskets.Where(x => x.Years != null))
                    {
                        foreach (var owner in basket.Years
                            .Where(x => x.Year == FantacalcioTime.ActualYear)
                            .SelectMany(x => x.Teams)
                            .Select(x => x.Owner))
                        {
                            await _teamCalculator.BuildRequestAsync(FantacalcioTime.ActualYear, nextDay, LeagueType.Null, LeagueSetting.Default, true, true, true);
                            var teamKey = new TeamDayKey
                            {
                                Year = FantacalcioTime.ActualYear,
                                Day = nextDay,
                                Email = owner,
                                Group = group.Id,
                                BasketId = basket.Id
                            };
                            var team = await _teamRepository.GetAsync(new TeamKey
                            {
                                Group = group.Id,
                                BasketId = basket.Id,
                                Email = owner,
                                Year = FantacalcioTime.ActualYear
                            });
                            if (team != null)
                            {
                                var players = _teamCalculator.Enrich(team.ActivePlayers);
                                var messages = new List<ChatMessage>
                                {
                                    new SystemChatMessage($"Sei un giornalista sportivo e devi scrivere un articolo sulla miglior formazione possibile che può fare giocare questo allenatore. Ti troverai la lista di tutti i giocatori, che chance hanno di giocare e che calendario si ritrovano ad avere, in più ci sarà anche la classifica di serie a per capire la forza delle squadre in cui militano. Sono validi solo i seguenti moduli: 3-4-3 (di solito il preferito), 4-3-3, 4-4-2, 3-5-2, 4-5-1, 3-6-1, 5-4-1, 5-3-2."),
                                    new SystemChatMessage($"L'output deve usare i tag HTML classici per un articolo sul web. Non usare backtips prima, mettimi solo l'HTMl dell'articolo e non mettere il titolo. Crea almeno gli 11 titolari e spiega perché dovrebbero essere titolari, e per ogni settore del campo: difesa, centrocampo e attacco dai almeno due sostituti spiegandone il motivo."),
                                    new SystemChatMessage($"Usa solo questi giocatori per comporre la squadra: {PlayersAsString(players)}"),
                                    new SystemChatMessage($"giornata serie a) {currentDay.ToJson()}"),
                                    new SystemChatMessage($"classifica serie a) {serieARank.ToJson()}")
                                };
                                var newsContent = string.Empty;
                                try
                                {
                                    newsContent = string.Join(string.Empty, await RequestAsync(messages, false).ToListAsync());
                                }
                                catch
                                {
                                    newsContent = string.Join(string.Empty, await RequestAsync(messages, true).ToListAsync());
                                }
                                if (await _teamWithAiRepository.ExistAsync(teamKey))
                                {
                                    await _teamWithAiRepository.UpdateAsync(teamKey, new TeamWithAi
                                    {
                                        Message = newsContent
                                    });
                                }
                                else
                                {
                                    await _teamWithAiRepository.InsertAsync(teamKey, new TeamWithAi
                                    {
                                        Message = newsContent
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
        public string PlayersAsString(IEnumerable<EnrichedPlayer> players)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine();
            var counter = 1;
            foreach (var player in players)
            {
                stringBuilder.Append($"Il giocatore numero {counter} di nome: {player.Current.Name}. ");
                stringBuilder.Append($" con il ruolo: {player.Current.Role}. ");
                stringBuilder.Append($"La sua squadra reale: {player.Current.RealTeam.Name}. ");
                if (player.Chance.Status == ChanceType.Injury)
                    stringBuilder.Append("Risulta infortunato. ");
                else
                {
                    stringBuilder.Append($"Secondo la gazzetta {(player.Chance.Gazzetta ? "giocherà titolare" : "non giocherà")}. ");
                    stringBuilder.Append($"Secondo la fantacalcio {(player.Chance.Fantagazzetta ? "giocherà titolare" : "non giocherà")}. ");
                }
                stringBuilder.AppendLine();
                counter++;
            }
            return stringBuilder.ToString();
        }
        private async IAsyncEnumerable<string> RequestAsync(IList<ChatMessage> messages, bool withDelay)
        {
            if (withDelay)
                await Task.Delay(60_000);
            var apiKey = _configuration["OpenAi:ApiKey"];
            var uriOai = _configuration["OpenAi:Endpoint"];
            var model = _configuration["OpenAi:ModelName"];
            var openAiClientFactory = new AzureOpenAIClient(new Uri(uriOai), new ApiKeyCredential(apiKey));
            var chatCompletionsOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096
            };
            var openAiClient = openAiClientFactory.GetChatClient(model);

            var requests = openAiClient.CompleteChatStreamingAsync(messages, chatCompletionsOptions);
            await foreach (var chatUpdate in requests)
            {
                if (chatUpdate.FinishReason == ChatFinishReason.ToolCalls)
                {
                    break;
                }
                foreach (var contentPart in chatUpdate.ContentUpdate)
                {
                    if (!string.IsNullOrWhiteSpace(contentPart.Text))
                        yield return contentPart.Text;
                }
            }
        }
        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<TeamHelperJob>(exception);
    }
}
