using Fantasoccer;
using System.Timers;

namespace FantacalcioV4.Engine
{
    public class DailyRestoringJob : IBackgroundJob
    {
        private readonly Manager Manager;
        private readonly IRepository<FantacalcioLog, string> _logger;
        public DailyRestoringJob(Manager manager, Logger logger)
        {
            Manager = manager;
            _logger = logger;
        }
        public async Task ActionToDoAsync()
        {
            foreach (var group in await Manager.Group.ListAsync())
            {
                var groupFromMemory = await new GroupKey() { Name = group }.InstanceAsync().NoContext();
                List<Task> leaguesForGroup = new();
                foreach (var league in groupFromMemory.Leagues)
                {
                    //if (!league.IsMain && league.Name.StartsWith("Champions"))
                    leaguesForGroup.Add(CalculateAsync());

                    async Task CalculateAsync()
                    {
                        var serieADay = await Manager.Live.GetCurrentSerieADayAsync(FantacalcioTime.ActualYear).NoContext();

                        if (serieADay < 38)
                        {
                            await new CalendarKey()
                            {
                                GroupName = groupFromMemory.Name,
                                LeagueName = league.Name,
                                Year = FantacalcioTime.ActualYear
                            }.RestoreAsync().NoContext();
                            await new RankKey()
                            {
                                GroupName = groupFromMemory.Name,
                                LeagueName = league.Name,
                                Year = FantacalcioTime.ActualYear
                            }.RestoreAsync().NoContext();
                            var lastLeagueYear = league.Years.OrderByDescending(x => x.Year).First();
                            if (lastLeagueYear.Type == LeagueType.Cup && serieADay >= 22)
                                await CalculateNextChampionsCalendarAsync(lastLeagueYear, serieADay).NoContext();
                            var calendar = await new CalendarKey { GroupName = groupFromMemory.Name, Year = FantacalcioTime.ActualYear, LeagueName = league.Name }.InstanceAsync().NoContext();
                            var day = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay + 1);
                            if (day != default)
                            {
                                foreach (var gamesByGroup in day.Games)
                                {
                                    foreach (var game in gamesByGroup.Value)
                                    {
                                        await Task.WhenAll(SaveTeamAsync(game.MiniHome.Id), SaveTeamAsync(game.MiniAway.Id)).NoContext();

                                        async Task SaveTeamAsync(string teamId)
                                        {
                                            var actualTeamContext = new TeamContext(game.Id, serieADay + 1);
                                            if (!await actualTeamContext.ExistAsync().NoContext())
                                            {
                                                var lastTeam = await new TeamContext(game.Id, serieADay).GetAsync().NoContext();
                                                if (lastTeam != default)
                                                    await actualTeamContext.SaveAsync(lastTeam).NoContext();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                await Task.WhenAll(leaguesForGroup).NoContext();
                await Logger.MessageAsync<DailyRestoringJob>($"{group} restored.").NoContext();
            }
        }
        private static async Task CalculateNextChampionsCalendarAsync(AnnualLeague league, int serieADay)
        {
            //todo da capire se funziona il calcolo della champions
            List<string> nextTeams = new();
            var calendar = await new CalendarKey { GroupName = league.GroupName, LeagueName = league.Name, Year = league.Year }.InstanceAsync().NoContext();
            if (serieADay == 22)
            {
                var rank = await new RankKey { GroupName = league.GroupName, LeagueName = league.Name, Year = league.Year }.InstanceAsync().NoContext();
                foreach (var teamInGroup in rank.OrderedRankedTeams)
                    nextTeams.AddRange(teamInGroup.Take(2).Select(x => x.Username));
            }
            else if (serieADay == 28 || serieADay == 34)
            {
                var first = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay - 3);
                var second = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay);
                foreach (var game in first.Games.SelectMany(x => x.Value))
                {
                    var secondGame = second.Games.SelectMany(x => x.Value).FirstOrDefault(x => x.MiniHome.Id == game.MiniAway.Id);
                    int goalA = game.Result.HomeGoal + secondGame.Result.AwayGoal;
                    int goalB = game.Result.AwayGoal + secondGame.Result.HomeGoal;
                    if (goalA == goalB)
                    {
                        goalA = game.Result.HomeGoal + 2 * secondGame.Result.AwayGoal;
                        goalB = 2 * game.Result.AwayGoal + secondGame.Result.HomeGoal;
                    }
                    if (goalA > goalB)
                        nextTeams.Add(game.HomeUsername);
                    else if (goalB > goalA)
                        nextTeams.Add(game.AwayUsername);
                    else
                    {
                        double pointA = game.Result.Home.Value + secondGame.Result.Away.Value;
                        double pointB = game.Result.Away.Value + secondGame.Result.Home.Value;
                        if (pointA > pointB)
                            nextTeams.Add(game.HomeUsername);
                        else if (pointB > pointA)
                            nextTeams.Add(game.AwayUsername);
                        else
                        {
                            var rank = await new RankKey { GroupName = league.GroupName, LeagueName = league.Name, Year = league.Year }.InstanceAsync().NoContext();
                            double valueA = rank.RankedTeams.FirstOrDefault(x => x.TeamId == game.MiniHome.Id).ValuePoint;
                            double valueB = rank.RankedTeams.FirstOrDefault(x => x.TeamId == game.MiniAway.Id).ValuePoint;
                            if (valueA > valueB)
                                nextTeams.Add(game.HomeUsername);
                            else if (valueB > valueA)
                                nextTeams.Add(game.AwayUsername);
                            else
                                nextTeams.Add(Alea.GetNumber(100) < 50 ? game.HomeUsername : game.AwayUsername);
                        }
                    }
                }
            }
            if (nextTeams.Count > 0)
            {
                //var dailies = calendar.Days.Where(x => x.SerieADay > 22).ToList();
                //foreach (var day in dailies)
                //    calendar.Days.Remove(day);
                for (int adder = 1; nextTeams.Count == 2 ? adder <= 1 : adder <= 2; adder++)
                {
                    int nextSerieADay = serieADay + adder * 3;
                    calendar.Days.Add(new Daily(nextSerieADay, new Dictionary<int, List<PlayedGame>>()));
                    calendar.Days.Last().Games.Add(0, new());
                    for (int i = 0; i < nextTeams.Count / 2; i++)
                    {
                        calendar.Days.Last().Games.Last().Value.Add(new PlayedGame(
                            FantacalcioId.GetGameId(league.GroupName, league.Year, league.Name,
                                    nextSerieADay,
                                    adder == 1 ? nextTeams[i] : nextTeams[nextTeams.Count - 1 - i],
                                    adder == 2 ? nextTeams[i] : nextTeams[nextTeams.Count - 1 - i])
                        ));
                    }
                }
                await new CalendarContext(league.GroupName, league.Year, league.Name).SaveAsync(calendar).NoContext();
            }
        }

        public Task OnException(Exception exception)
             => Logger.CatchAsync<DailyRestoringJob>(exception);
    }
}