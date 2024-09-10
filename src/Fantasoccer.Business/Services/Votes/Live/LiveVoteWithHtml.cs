using System.Text.RegularExpressions;
using RepositoryFramework;

namespace Fantasoccer.Business.IngestionData
{
    internal sealed class LiveVoteWithHtml : ILiveVote
    {
        private readonly HttpClient _httpClient;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        public LiveVoteWithHtml(IHttpClientFactory httpClientFactory,
            IRepository<RealCalendar, int> realCalendar)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _realCalendar = realCalendar;
        }
        public async Task<List<VotedRealPlayer>> GetAsync(int year, int day)
        {
            var players = new List<VotedRealPlayer>();
            return await GetLiveAsync(year + 6, day).ToListAsync();
        }
        private static readonly Regex s_regexForSplitting = new("<a class=\"match-score");

        private async IAsyncEnumerable<string> GetGamesAsync(int year, int day)
        {
            var response = await _httpClient.GetAsync($"https://www.fantacalcio.it/serie-a/calendario/{day}");
            var responseAsString = await response.Content.ReadAsStringAsync();
            foreach (var match in responseAsString.Split("<a class=\"match-score").Skip(1))
            {
                var uriWithLong = match.Split("href=\"");
                if (uriWithLong.Length > 1)
                {
                    var uri = uriWithLong[1].Split('"')[0];
                    if (uri.StartsWith("https://www.fantacalcio.it/serie-a/calendario"))
                    {
                        yield return uri;
                    }
                }
            }
        }
        private static readonly Regex DataCountEventRegex = new("data-count=\"[0-9]{1,4}\"");
        private static readonly Regex DataIdEventRegex = new("data-event-id=\"[0-9]{1,4}\"");
        private async IAsyncEnumerable<VotedRealPlayer> GetLiveAsync(int year, int day)
        {
            await foreach (var gameUri in GetGamesAsync(year, day))
            {
                await Task.Delay(500);
                var response = await _httpClient.GetAsync($"{gameUri}/voti");
                var responseAsString = await response.Content.ReadAsStringAsync();
                foreach (var playerAsHtmlWithNoUlCheck in responseAsString.Split("class=\"player-item").Skip(1))
                {
                    var playerAsHtml = playerAsHtmlWithNoUlCheck.Split("</ul>")[0];
                    var playerNameAsHtml = playerAsHtml.Split("player-name");
                    if (playerNameAsHtml.Length < 2)
                        continue;
                    var playerName = playerAsHtml.Split("player-name")[1].Split('>')[2].Split('<')[0].Trim();
                    if (string.IsNullOrWhiteSpace(playerName))
                        continue;
                    var roleAsHtml = playerAsHtml.Split("class=\"role\"")[1].Split('>')[0];
                    var playerRole = Role.Undefined;
                    if (roleAsHtml.Contains("data-value=\"a\""))
                    {
                        playerRole = Role.Forward;
                    }
                    else if (roleAsHtml.Contains("data-value=\"c\""))
                    {
                        playerRole = Role.Midfielder;
                    }
                    else if (roleAsHtml.Contains("data-value=\"d\""))
                    {
                        playerRole = Role.Defensor;
                    }
                    else if (roleAsHtml.Contains("data-value=\"p\""))
                    {
                        playerRole = Role.GoalKeeper;
                    }
                    var playerVoteAsHtml = playerAsHtml.Split("class=\"player-grade")[1].Split('"')[2].Replace(',', '.');
                    var noVote = playerVoteAsHtml == "--" || playerVoteAsHtml == "-";
                    var playerVote = noVote ? 0 : float.Parse(playerVoteAsHtml);
                    if (playerVote < 0)
                        playerVote *= -1;
                    var isIn = !playerAsHtml.Contains("player-out");
                    var currentVote = new VotedRealPlayer
                    {
                        IsActive = true,
                        Name = playerName,
                        Role = playerRole,
                        Vote = new()
                        {
                            Value = playerVote,
                            HasVote = !noVote,
                            IsFinal = false,
                            Role = playerRole,
                            Status = Behaviour.Nothing,
                            IsIn = isIn,
                            IsOut = !isIn,
                        }
                    };
                    foreach (var eventFigureHtml in playerAsHtml.Split("<figure").Skip(1))
                    {
                        var singleEventHtml = eventFigureHtml.Split('>')[0];
                        var eventCount = int.Parse(DataCountEventRegex.Match(singleEventHtml).Value.Split('"')[1]);
                        var eventId = int.Parse(DataIdEventRegex.Match(singleEventHtml).Value.Split('"')[1]);
                        switch (eventId)
                        {
                            case 1:
                                currentVote.Vote.Status = Behaviour.YellowCard;
                                break;
                            case 2:
                                currentVote.Vote.Status = Behaviour.RedCard;
                                break;
                            case 3:
                                currentVote.Vote.Goal++;
                                break;
                            case 4:
                                currentVote.Vote.SufferedGoal++;
                                break;
                            case 5:
                            case 6:
                            case 21:
                            case 22:
                            case 23:
                            case 24:
                                currentVote.Vote.Assist++;
                                break;
                            case 7:
                                currentVote.Vote.StoppedPenalty++;
                                break;
                            case 8:
                                currentVote.Vote.WrongedPenalty++;
                                break;
                            case 9:
                                currentVote.Vote.Penalty++;
                                break;
                            case 10:
                                currentVote.Vote.OwnGoal++;
                                break;
                            case 26:
                                currentVote.Vote.ManOfTheMatch = true;
                                break;
                        }
                    }
                    yield return currentVote;
                }
            }
        }
    }
}
