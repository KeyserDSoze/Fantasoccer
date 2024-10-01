namespace Fantasoccer.Business
{
    internal sealed class RankCalculator : IRankCalculator
    {
        public Rank AddDay(LeagueSetting leagueSetting, Rank rank, Dictionary<string, Day> rounds)
        {
            foreach (var day in rounds)
            {
                var teams = rank.RankedTeams[day.Key];
                foreach (var game in day.Value.Games)
                {
                    var homeTeam = teams.First(x => x.Owner == game.HomeOwner);
                    var awayTeam = teams.First(x => x.Owner == game.AwayOwner);
                    AddDay(homeTeam, true, game.Result, leagueSetting);
                    AddDay(awayTeam, false, game.Result, leagueSetting);
                }
            }
            return rank;
        }

        public bool AddDay(RankedTeam team, bool isInHome, GameResult? result, LeagueSetting leagueSetting)
        {
            if (result?.IsCancelled == true)
                return true;
            if (result == default || !result.HasValue)
                return false;

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
                if (!leagueSetting.RankWithValuePoints)
                    team.Point += leagueSetting.PointForVictory;
                else
                    team.Point += pointTeam + leagueSetting.PointForVictory;
            }
            else if (goalOpponent > goalTeam)
            {
                team.Defeats++;
                if (!leagueSetting.RankWithValuePoints)
                    team.Point += leagueSetting.PointForDefeat;
                else
                    team.Point += pointTeam + leagueSetting.PointForDefeat;
            }
            else
            {
                team.Draws++;
                if (!leagueSetting.RankWithValuePoints)
                    team.Point += leagueSetting.PointForDraw;
                else
                    team.Point += pointTeam + leagueSetting.PointForDraw;
            }
            team.PlusMoney += leagueSetting.MoneyForGoal * goalTeam;
            team.PlusMoney += leagueSetting.MoneyForSufferedGoal * goalOpponent;
            return true;
        }
    }
}
