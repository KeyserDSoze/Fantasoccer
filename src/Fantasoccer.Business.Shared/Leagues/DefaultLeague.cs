using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class DefaultLeague : ILeagueCalculator
    {
        private readonly IRankCalculator _rankCalculator;
        public DefaultLeague(IRankCalculator rankCalculator)
        {
            _rankCalculator = rankCalculator;
        }
        public Calendar AddDaysInLeague(int year, AnnualLeague league, List<AnnualTeam> teams, Rank rank, Calendar calendar)
        {
            throw new NotImplementedException();
        }

        public Calendar GetRandomCalendar(int year, AnnualLeague league, IEnumerable<AnnualTeam> teams)
        {
            throw new NotImplementedException();
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
    }
}
