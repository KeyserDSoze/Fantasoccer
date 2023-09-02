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
        public SerieACalendar(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
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
    }
}
