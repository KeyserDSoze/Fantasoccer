using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    public interface ISerieACalendar
    {
    }
    internal sealed class SerieACalendar : ISerieACalendar
    {
        private static readonly Regex DailyRegex = new("</sup> giornata");
        private static readonly Regex ResultDateRegex = new("class=\"Results_date");
        private static readonly Regex ResultRowRegex = new("class=\"ResultsRow_row");
        private static readonly Regex RowCellRegex = new("<div role=\"cell\">");
        private readonly IRepository<FantasoccerLog, string> _logger;
        private readonly HttpClient HttpClient;
        public SerieACalendarJob(IRepository<FantasoccerLog, string> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            HttpClient = httpClientFactory.CreateClient("base");
        }

        public async Task ActionToDoAsync()
        {
            DateTime theGameDay = DateTime.UtcNow;
            List<RealDay> days = new();
            string normalResponse = await HttpClient.GetStringAsync(
                string.Format("https://www.corrieredellosport.it/live/calendario-serie-a",
                FantacalcioTime.ActualLabelYear.Replace("/", "-")))
                                            .NoContext();
            int actualYear = FantacalcioTime.ActualYear;
            int day = 1;
            foreach (string dayAsString in DailyRegex.Split(normalResponse).Skip(1))
            {
                if (day >= 39)
                    break;
                List<RealGame> games = new();
                int count = 0;
                foreach (string dateAsString in ResultDateRegex.Split(dayAsString).Skip(1))
                {
                    string theDate = dateAsString.Split('>')[1].Split('<')[0].Split(' ')[1];
                    DateTime dailyDate = DateTime.ParseExact(theDate, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    foreach (string gameAsString in ResultRowRegex.Split(dateAsString).Skip(1))
                    {
                        if (count >= 10)
                            break;
                        string[] gameSplitted = RowCellRegex.Split(gameAsString);
                        string firstTeam = gameSplitted[1].Split('>')[^2].Split('<').FirstOrDefault().Trim();
                        string secondTeam = gameSplitted[3].Split('<').FirstOrDefault().Trim();
                        string result = gameSplitted[2].Contains("time") ?
                            gameSplitted[2].Split('>')[1].Split('<').FirstOrDefault() :
                            gameSplitted[2].Split('<').FirstOrDefault();
                        int? aResult = null;
                        int? bResult = null;
                        bool delayed = false;
                        DateTime date = dailyDate;
                        if (result.Contains(':'))
                        {
                            string[] hoursAndMinutes = result.Split(':');
                            date = date.AddHours(int.Parse(hoursAndMinutes.First()));
                            date = date.AddMinutes(int.Parse(hoursAndMinutes.Last()));
                            if (date < FantacalcioTime.ItalyNow)
                                delayed = true;
                        }
                        else
                        {
                            string[] splittedResult = result.Split('-');
                            aResult = int.Parse(splittedResult.First());
                            bResult = int.Parse(splittedResult.Last());
                        }
#warning Ricordarsi che così il rinviato non funziona, se ricalcolo i voti si rompe

                        RealGame realGame = new(firstTeam, secondTeam, firstTeam.Substring(0, 3).ToLower(), secondTeam.Substring(0, 3).ToLower())
                        {
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
                days.Add(new(actualYear, day, games));
                day++;
            }
            var calendar = await new SerieACalendarContext(actualYear).GetAsync().NoContext();
            if (calendar == null)
                calendar = new(actualYear, days);
            foreach (var daily in calendar.Days)
            {
                var dailyFromSource = days.FirstOrDefault(x => x.SerieADay == daily.SerieADay);
                foreach (var game in daily.Games)
                {
                    var gameFromSource = dailyFromSource.Games.FirstOrDefault(x => x.AbbreviatedHome == game.AbbreviatedHome && x.AbbreviatedAway == game.AbbreviatedAway);
                    if (gameFromSource == null)
                    {
                        var doubled = dailyFromSource.Games.GroupBy(x => x.AbbreviatedHome).FirstOrDefault(x => x.Count() > 1)?.Last();
                        if (doubled != null)
                        {
                            dailyFromSource.Games.Remove(doubled);
                            dailyFromSource.Games.Add(gameFromSource = new RealGame(game.Home, game.Away, game.AbbreviatedHome, game.AbbreviatedAway));
                        }
                    }
                    game.HomeGoal = gameFromSource.HomeGoal;
                    game.AwayGoal = gameFromSource.AwayGoal;
                    if (game.Date > FantacalcioTime.ItalyNow || gameFromSource.Delayed)
                    {
                        game.Date = gameFromSource.Date;
                        game.Delayed = gameFromSource.Delayed;
                    }

                }
            }
        }
    }
}
