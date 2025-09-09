using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class DefaultLeague : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;
        private readonly IRandomCalendar _randomCalendar;

        public DefaultLeague(IRankCalculator rankCalculator, IRandomCalendar randomCalendar)
        {
            _rankCalculator = rankCalculator;
            _randomCalendar = randomCalendar;
        }
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
        {
            throw new NotImplementedException();
        }

        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            var key = ((char)(64)).ToString();
            var allTeams = teams.ToList();
            var randomizedTeams = _randomCalendar.Randomize(allTeams);
            var calendar = new Calendar()
            {
                Year = year,
                Rounds = new()
            };

            var randomizedCalendar = _randomCalendar.GetCalendar(randomizedTeams.Count, 1);
            var numberOfDays = randomizedTeams.Count - 1;
            var homeAndAwayCounts = (38 - league.Settings.DelayedDay) / numberOfDays;
            var actualDay = 1;
            for (var homeAwayRound = 0; homeAwayRound < homeAndAwayCounts; homeAwayRound++)
            {
                if (!calendar.Rounds.ContainsKey(key))
                    calendar.Rounds.Add(key, []);
                var singleRound = calendar.Rounds[key];
                var oddRounds = homeAwayRound % 2 == 0;
                foreach (var rounds in randomizedCalendar.GroupBy(x => x.Day))
                {
                    foreach (var games in rounds.GroupBy(x => x.Round))
                    {
                        var serieADay = actualDay + league.Settings.DelayedDay;
                        var isCancelled = actualDay <= league.Settings.CancelledDay;
                        var day = new Day { Games = [], SerieADay = serieADay, Number = actualDay };
                        singleRound.Add(day);
                        var gameCount = 1;
                        foreach (var game in games)
                        {
                            var home = oddRounds ? randomizedTeams[game.Game.ATeam] : randomizedTeams[game.Game.BTeam];
                            var away = oddRounds ? randomizedTeams[game.Game.BTeam] : randomizedTeams[game.Game.ATeam];
                            day.Games.Add(new CalendarGame
                            {
                                Id = Guid.NewGuid().ToString(),
                                Number = gameCount,
                                Home = home.Name,
                                HomeOwner = home.Owner,
                                Away = away.Name,
                                AwayOwner = away.Owner,
                                Result = isCancelled ? new GameResult { IsCancelled = true } : null
                            });
                            gameCount++;
                        }
                        actualDay++;
                    }
                }
            }
            return calendar;
        }

        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, List<string>? avoidCalculationRounds = null)
        {
            var rank = new Rank() { RankedTeams = new(), SerieADay = 0 };
            if (calendar != null)
            {
                foreach (var round in calendar.Rounds)
                {
                    if (avoidCalculationRounds != null && avoidCalculationRounds.Any(x => x == round.Key))
                        continue;
                    if (!rank.RankedTeams.ContainsKey(round.Key))
                    {
                        rank.RankedTeams.Add(round.Key,
                            round.Value.First().Games.SelectMany(x => new RankedTeam[] { new RankedTeam { Name = x.Home, Owner = x.HomeOwner }, new RankedTeam { Name = x.Away, Owner = x.AwayOwner } }).ToList());
                    }
                    var teams = rank.RankedTeams[round.Key];
                    foreach (var day in round.Value)
                    {
                        foreach (var game in day.Games)
                        {
                            var homeTeam = teams.First(x => x.Owner == game.HomeOwner);
                            var awayTeam = teams.First(x => x.Owner == game.AwayOwner);
                            if (_rankCalculator.AddDay(homeTeam, true, game.Result, leagueSetting) && rank.SerieADay < day.SerieADay)
                                rank.SerieADay = day.SerieADay;
                            if (_rankCalculator.AddDay(awayTeam, false, game.Result, leagueSetting) && rank.SerieADay < day.SerieADay)
                                rank.SerieADay = day.SerieADay;
                        }
                    }
                }
                foreach (var roundKey in calendar.Rounds.Select(x => x.Key))
                {
                    if (avoidCalculationRounds != null && avoidCalculationRounds.Any(x => x == roundKey))
                        continue;
                    rank.RankedTeams[roundKey] = rank.RankedTeams[roundKey].OrderByDescending(x => x.Point).ThenByDescending(x => x.ValuePoint).ToList();
                }
            }
            return rank;
        }

        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players, ITeamCalculator teamCalculator)
            => GetRightFormation(teamCalculator.Enrich(players));
        public IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players)
        {
            if (!players.Any())
                return Array.Empty<EnrichedPlayer>();
            var allPlayers = players.ToList();
            var playersInField = allPlayers.Where(x => x.Current.Position >= FantaSoccerRole.GoalKeeper && x.Current.Position <= FantaSoccerRole.Forward).OrderBy(x => x.Current.Position).ToList();
            var playersAsSubstitute = allPlayers.Where(x => x.Current.Position > FantaSoccerRole.Forward && x.Current.Position <= FantaSoccerRole.SecondBackupForward).OrderBy(x => x.Current.Position).ToList();
            for (var i = 0; i < playersInField.Count; i++)
            {
                if (playersInField[i].Current.Position >= FantaSoccerRole.GoalKeeper
                    && playersInField[i].Current.Position <= FantaSoccerRole.Forward
                    && playersInField[i].Vote?.HasVote != true)
                {
                    for (var j = 0; j < playersAsSubstitute.Count; j++)
                    {
                        if (playersAsSubstitute[j].Current.Position > FantaSoccerRole.Forward
                            && playersAsSubstitute[j].Current.Position <= FantaSoccerRole.SecondBackupForward
                            && playersAsSubstitute[j].CurrentPosition > FantaSoccerRole.Forward
                            && playersAsSubstitute[j].Current.Role == playersInField[i].Current.Role
                            && playersAsSubstitute[j].Vote?.HasVote == true)
                        {
                            var positionSwitcher = playersInField[i].CurrentPosition;
                            playersInField[i].CurrentPosition = playersAsSubstitute[j].CurrentPosition;
                            playersAsSubstitute[j].CurrentPosition = positionSwitcher;
                            break;
                        }
                    }
                }
            }
            playersInField.AddRange(playersAsSubstitute);
            return playersInField;
        }
        public List<LeagueWinner> GetTheYearlyWinners(Rank rank, Calendar calendar)
        {
            var winners = new List<LeagueWinner>();
            if (rank != null)
            {
                var winner = rank.RankedTeams.First().Value.OrderByDescending(x => x.Point).ThenByDescending(x => x.ValuePoint).ThenByDescending(x => x.Goal).First().Owner;
                winners.Add(new LeagueWinner { Owner = winner, Round = "@" });
            }
            return winners;
        }
    }
}
