namespace Fantasoccer.Business
{
    internal sealed class RankCalculator : IRankCalculator
    {
        public Rank GetRank(LeagueSetting leagueSetting, Calendar calendar, LeagueType type)
        {
            var rank = new Rank() { RankedTeams = new() };
            foreach (var round in calendar.Rounds)
            {
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
                        AddDay(homeTeam, true, game.Result, leagueSetting);
                        AddDay(awayTeam, false, game.Result, leagueSetting);
                    }
                }
            }
            return rank;
        }
        private void AddDay(RankedTeam team, bool isInHome, GameResult? result, LeagueSetting leagueSetting)
        {
            if (result == default || !result.HasValue || result.IsCancelled)
                return;

            if (isInHome)
                SetPointAndGoal(result.Home.Value, result.HomeGoal, result.Away.Value, result.AwayGoal);
            else
                SetPointAndGoal(result.Away.Value, result.AwayGoal, result.Home.Value, result.HomeGoal);

            float pointTeam;
            float pointOpponent;
            int goalTeam;
            int goalOpponent;
            void SetPointAndGoal(float point, int goal, float opponentPoint, int opponentGoal)
            {
                pointTeam = point;
                pointOpponent = opponentPoint;
                goalTeam = goal;
                goalOpponent = opponentGoal;
            }

            team.Goal += goalTeam;
            team.SufferedGoal += goalOpponent;
            team.ValuePoint += pointTeam;
            team.SufferedValuePoint += pointOpponent;
            if (goalTeam > goalOpponent)
            {
                team.Victories++;
                team.Point += leagueSetting.PointForVictory;
            }
            else if (goalOpponent > goalTeam)
            {
                team.Defeats++;
                team.Point += leagueSetting.PointForDefeat;
            }
            else
            {
                team.Draws++;
                team.Point += leagueSetting.PointForDraw;
            }
            team.PlusMoney += leagueSetting.MoneyForGoal * goalTeam;
            team.PlusMoney += leagueSetting.MoneyForSufferedGoal * goalOpponent;
        }
    }
}
