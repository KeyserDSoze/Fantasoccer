namespace Fantasoccer
{
    public record GameResult()
    {
        public Point Home { get; set; }
        public Point Away { get; set; }
        public bool HasValue => Home?.Value > 0 || Away?.Value > 0;
        public GameResultType Value =>
            this switch
            {
                { HomeGoal: var h, AwayGoal: var a } when h > a => GameResultType.HomeWon,
                { HomeGoal: var h, AwayGoal: var a } when h < a => GameResultType.AwayWon,
                { HomeGoal: var h, AwayGoal: var a } when a == h => GameResultType.Tie,
                _ => GameResultType.Tie
            };
        public int HomeGoal { get; set; }
        public int AwayGoal { get; set; }
        public (int Home, int Away) CalculateGoals(LeagueSetting settings)
        {
            return (HomeGoal = GetGoals(Home.Value, Away.Value), AwayGoal = GetGoals(Away.Value, Home.Value));

            int GetGoals(float value, float opponentValue)
            {
                int goalTeam = 0;
                if (value - settings.PointForFirstGoal >= 0)
                    goalTeam = 1 + (int)((value - settings.PointForFirstGoal) / settings.PointForNextGoal);
                if (settings.DifferencePointForOwnGoal > 0 && value < 66 && opponentValue <= (65.5 - settings.DifferencePointForOwnGoal) && value - opponentValue >= settings.DifferencePointForOwnGoal)
                    goalTeam++;
                return goalTeam;
            }
        }
    }
}
