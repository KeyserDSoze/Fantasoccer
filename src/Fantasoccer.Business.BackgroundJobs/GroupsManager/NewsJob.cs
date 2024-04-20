using System.Reflection;
using System.Text.Json;
using System.Timers;
using Azure;
using Azure.AI.OpenAI;
using Fantasoccer.Domain;
using Microsoft.Extensions.Configuration;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager
{
    internal class NewsJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRankCalculator _rankCalculator;
        private readonly IRandomCalendar _randomCalendar;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<Rank, YearDayLeague> _rankDayRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IRepository<NewsWrapper, YearDayGroup> _newsRepository;
        private readonly IConfiguration _configuration;

        public NewsJob(IFantasoccerLogger logger,
            ITeamCalculator teamCalculator,
            IRankCalculator rankCalculator,
            IRandomCalendar randomCalendar,
            IRepository<Group, string> groups,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<Rank, YearDayLeague> rankDayRepository,
            IRepository<Rank, LeagueKey> rankRepository,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar,
            IRepository<NewsWrapper, YearDayGroup> newsRepository,
            IConfiguration configuration)
        {
            _logger = logger;
            _teamCalculator = teamCalculator;
            _rankCalculator = rankCalculator;
            _randomCalendar = randomCalendar;
            _groups = groups;
            _calendarRepository = calendarRepository;
            _rankDayRepository = rankDayRepository;
            _rankRepository = rankRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
            _newsRepository = newsRepository;
            _configuration = configuration;
        }
        public async Task ActionToDoAsync()
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var groups = await _groups.ToListAsEntityAsync();
                var lastSerieADay = realCalendar.LastDay?.SerieADay ?? 1;
                foreach (var group in groups)
                {
                    var groupNewsKey = new YearDayGroup { Year = actualYear, Day = lastSerieADay, Group = group.Id };
                    if (!await _newsRepository.ExistAsync(groupNewsKey))
                    {
                        var news = new NewsWrapper { Day = lastSerieADay, Year = actualYear, News = new List<News>() };
                        foreach (var league in group.Leagues.Where(x => x.Years.Exists(t => t.Year == actualYear)))
                        {
                            var leagueKey = new LeagueKey { Group = group.Id, League = league.Id, Year = actualYear };
                            var calendar = await _calendarRepository.GetAsync(leagueKey).NoContext();
                            if (calendar != null)
                            {
                                var rank = await _rankRepository.GetAsync(leagueKey).NoContext();
                                if (rank != null && rank.SerieADay == lastSerieADay)
                                {
                                    var days = calendar.Rounds.Where(x => x.Value.Any(t => t.SerieADay == lastSerieADay)).ToDictionary();
                                    if (days.Count > 0)
                                    {
                                        var hasEnded = false;
                                        if (league.Type == LeagueType.League)
                                        {
                                            if (lastSerieADay == 38)
                                            {
                                                hasEnded = true;
                                            }
                                            else
                                            {
                                                var possiblePoints = (38 - lastSerieADay) * 3;
                                                var first = rank.RankedTeams.SelectMany(x => x.Value).OrderByDescending(x => x.Point).First();
                                                var second = rank.RankedTeams.SelectMany(x => x.Value).OrderByDescending(x => x.Point).Skip(1).First();
                                                if (first.Point - second.Point > possiblePoints)
                                                    hasEnded = true;
                                            }
                                        }
                                        else
                                        {
                                            hasEnded = days.SelectMany(x => x.Value).SelectMany(x => x.Games).Count() == 1;
                                        }
                                        var messages = new List<ChatRequestMessage>
                                        {
                                            new ChatRequestSystemMessage($"Sei un giornalista sportivo e devi scrivere un articolo di calcio basato sui risultati in formato json che ti metterò nel punto 1). Basato sul rank in json che ti metterò nel punto 2). Fai un articolo pieno di enfasi, in cui si nota molto bene l'emozione. Mi raccomando, non mettere il titolo, scrivi solo l'articolo."),
                                            new ChatRequestSystemMessage($"L'output deve usare i tag HTML classici per un articolo sul web. Non usare backtips prima, mettimi solo l'HTMl dell'articolo e non mettere il titolo. {(hasEnded ? "Il campionato è finito. Celebra il campione.".ToUpper() : "Il campionato non è ancora finito.".ToUpper())}"),
                                            new ChatRequestUserMessage($"1) {days.ToJson()}"),
                                            new ChatRequestUserMessage($"2) {rank.ToJson()}")
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
                                        if (!string.IsNullOrWhiteSpace(newsContent))
                                        {
                                            var titleContent = string.Empty;
                                            messages =
                                            [
                                                new ChatRequestSystemMessage($"Sei un giornalista sportivo e devi dare un titolo ad un articolo che hai scritto."),
                                                new ChatRequestUserMessage($"{newsContent}")
                                            ];
                                            try
                                            {
                                                titleContent = string.Join(string.Empty, await RequestAsync(messages, false).ToListAsync());

                                            }
                                            catch
                                            {
                                                titleContent = string.Join(string.Empty, await RequestAsync(messages, false).ToListAsync());
                                            }
                                            news.News.Add(new News { League = league.Name, Title = titleContent, Content = newsContent, Date = DateTime.UtcNow });
                                        }
                                        //foreach (var remainDays in days.Values)
                                        //{
                                        //    foreach (var day in remainDays)
                                        //    {
                                        //        foreach (var game in day.Games)
                                        //        {

                                        //        }
                                        //    }
                                        //}
                                    }
                                }
                            }
                        }
                        if (news.News.Count > 0)
                            await _newsRepository.UpdateAsync(groupNewsKey, news);
                    }
                }
            }
        }
        private async IAsyncEnumerable<string> RequestAsync(IList<ChatRequestMessage> messages, bool withDelay)
        {
            if (withDelay)
                await Task.Delay(60_000);
            var apiKey = _configuration["OpenAi:ApiKey"];
            var uriOai = _configuration["OpenAi:Endpoint"];
            var model = _configuration["OpenAi:ModelName"];
            var client33 = new OpenAIClient(new Uri(uriOai), new AzureKeyCredential(apiKey));
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = model,
                MaxTokens = 4096
            };
            foreach (var message in messages)
                chatCompletionsOptions.Messages.Add(message);
            var tt = client33.GetChatCompletionsStreaming(chatCompletionsOptions);
            await foreach (var chatUpdate in tt)
            {
                if (!string.IsNullOrWhiteSpace(chatUpdate.ToolCallUpdate?.Id))
                {
                    break;
                }
                if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
                {
                    yield return chatUpdate.ContentUpdate;
                }
            }
        }
        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<NewsJob>(exception);
    }
}
