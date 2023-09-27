using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Fantasoccer.Business
{
    internal sealed class SerieACalendar : ISerieACalendar
    {
        private static readonly Regex s_dailyRegex = new("</sup> giornata");
        private static readonly Regex s_resultDateRegex = new("class=\"Results_date");
        private static readonly Regex s_resultRowRegex = new("class=\"ResultsRow_row");
        private static readonly Regex s_rowCellRegex = new("<div role=\"cell\">");
        private readonly HttpClient _httpClient;
        private readonly HttpClient _footballDataClient;
        public SerieACalendar(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _footballDataClient = httpClientFactory.CreateClient("footballdata");
        }

        public async Task<RealCalendar> GetAsync()
        {
            var calendar = new RealCalendar
            {
                Days = new List<RealDay>()
            };
            var normalResponse = await _httpClient.GetStringAsync("https://www.corrieredellosport.it/live/calendario-serie-a").NoContext();
            var actualYear = FantacalcioTime.ActualYear;
            var day = 1;
            foreach (var dayAsString in s_dailyRegex.Split(normalResponse).Skip(1))
            {
                if (day >= 39)
                    break;
                List<RealGame> games = new();
                var count = 0;
                foreach (var dateAsString in s_resultDateRegex.Split(dayAsString).Skip(1))
                {
                    var theDate = dateAsString.Split('>')[1].Split('<')[0].Split(' ')[1];
                    var dailyDate = DateTime.ParseExact(theDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    foreach (var gameAsString in s_resultRowRegex.Split(dateAsString).Skip(1))
                    {
                        if (count >= 10)
                            break;
                        var gameSplitted = s_rowCellRegex.Split(gameAsString);
                        var firstTeam = gameSplitted[1].Split('>')[^2].Split('<').FirstOrDefault().Trim();
                        var secondTeam = gameSplitted[3].Split('<').FirstOrDefault().Trim();
                        var result = gameSplitted[2].Contains("time") ?
                            gameSplitted[2].Split('>')[1].Split('<').FirstOrDefault() :
                            gameSplitted[2].Split('<').FirstOrDefault();
                        int? aResult = null;
                        int? bResult = null;
                        var delayed = false;
                        var date = dailyDate;
                        if (result?.Contains(':') == true)
                        {
                            var hoursAndMinutes = result.Split(':');
                            date = date.AddHours(int.Parse(hoursAndMinutes.First()));
                            date = date.AddMinutes(int.Parse(hoursAndMinutes.Last()));
                            if (date < FantacalcioTime.ItalyNow)
                                delayed = true;
                        }
                        else if (result != null)
                        {
                            var splittedResult = result.Split('-');
                            aResult = int.Parse(splittedResult.First());
                            bResult = int.Parse(splittedResult.Last());
                            date = date.AddHours(21);
                        }

                        var realGame = new RealGame()
                        {
                            Home = new RealTeam { Name = firstTeam },
                            Away = new RealTeam { Name = secondTeam },
                            Date = date,
                            Delayed = delayed,
                            HomeGoal = aResult,
                            AwayGoal = bResult
                        };
                        games.Add(realGame);
                        count++;
                    }
                }

                DateTime averageDate = new(games.Sum(x => x.Date.Value.Ticks) / games.Count);
                foreach (var game in games)
                {
                    if (game.Date.Value.Subtract(averageDate).TotalDays > 3)
                        game.Delayed = true;
                }
                calendar.Days.Add(new RealDay
                {
                    Games = games,
                    SerieADay = day,
                    Year = actualYear
                });
                day++;
            }
            return calendar;
        }
        public async Task<RealDay> GetDayAsync(int day)
        {
            var response = await _footballDataClient.GetFromJsonAsync<Root>($"v4/competitions/SA/matches?matchday={day}").NoContext();
            var realDay = new RealDay { Games = new List<RealGame>(), SerieADay = day, Year = FantacalcioTime.ActualYear };
            foreach (var match in response.Matches)
            {
                realDay.Games.Add(new RealGame
                {
                    Home = new RealTeam { Name = match.HomeTeam.ShortName },
                    Away = new RealTeam { Name = match.AwayTeam.ShortName },
                    Date = FantacalcioTime.ToItalyNow(match.UtcDate),
                    Delayed = false,
                    HomeGoal = match.Score.FullTime.Home ?? match.Score.HalfTime.Home,
                    AwayGoal = match.Score.FullTime.Away ?? match.Score.HalfTime.Away,
                });
            }
            DateTime averageDate = new(realDay.Games.Sum(x => x.Date.Value.Ticks) / realDay.Games.Count);
            foreach (var game in realDay.Games)
            {
                if (game.Date.Value.Subtract(averageDate).TotalDays > 3)
                    game.Delayed = true;
            }
            await Task.Delay(10);
            return realDay;
        }
        private class AwayTeam
        {
            [JsonPropertyName("shortName")]
            public string ShortName { get; set; }
        }

        private class FullTime
        {
            [JsonPropertyName("home")]
            public int? Home { get; set; }

            [JsonPropertyName("away")]
            public int? Away { get; set; }
        }

        private class HalfTime
        {
            [JsonPropertyName("home")]
            public int? Home { get; set; }

            [JsonPropertyName("away")]
            public int? Away { get; set; }
        }

        private class HomeTeam
        {
            [JsonPropertyName("shortName")]
            public string ShortName { get; set; }
        }

        private class Match
        {
            [JsonPropertyName("utcDate")]
            public DateTime UtcDate { get; set; }
            [JsonPropertyName("matchday")]
            public int Matchday { get; set; }
            [JsonPropertyName("lastUpdated")]
            public DateTime LastUpdated { get; set; }
            [JsonPropertyName("homeTeam")]
            public HomeTeam HomeTeam { get; set; }
            [JsonPropertyName("awayTeam")]
            public AwayTeam AwayTeam { get; set; }
            [JsonPropertyName("score")]
            public Score Score { get; set; }
        }

        private class Root
        {

            [JsonPropertyName("matches")]
            public List<Match> Matches { get; set; }
        }

        private class Score
        {
            [JsonPropertyName("fullTime")]
            public FullTime FullTime { get; set; }

            [JsonPropertyName("halfTime")]
            public HalfTime HalfTime { get; set; }
        }
    }
}
