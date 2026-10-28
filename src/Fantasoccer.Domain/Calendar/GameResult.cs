using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class GameResult
    {
        [JsonPropertyName("h")]
        public Point Home { get; set; }
        [JsonPropertyName("a")]
        public Point Away { get; set; }
        [JsonIgnore]
        public bool HasValue => Home is not null && Away is not null;
        [JsonPropertyName("i")]
        public bool IsCancelled { get; set; }
        [JsonIgnore]
        public GameResultType Value =>
            this switch
            {
                { HomeGoal: var h, AwayGoal: var a } when h > a => GameResultType.HomeWon,
                { HomeGoal: var h, AwayGoal: var a } when h < a => GameResultType.AwayWon,
                { HomeGoal: var h, AwayGoal: var a } when a == h => GameResultType.Tie,
                _ => GameResultType.Tie
            };
        [JsonPropertyName("g")]
        public int HomeGoal { get; set; }
        [JsonPropertyName("l")]
        public int AwayGoal { get; set; }
        public (int Home, int Away) CalculateGoals(LeagueSetting settings)
        {
            HomeGoal = GetGoals(Home.Value, Away.Value);
            AwayGoal = GetGoals(Away.Value, Home.Value);
            return (HomeGoal, AwayGoal);

            int GetGoals(float value, float opponentValue)
            {
                var goalTeam = 0;
                if (value - settings.PointForFirstGoal >= 0)
                    goalTeam = 1 + (int)((value - settings.PointForFirstGoal) / settings.PointForNextGoal);
                if (settings.DifferencePointForOwnGoal > 0 && value < 66 && opponentValue <= (65.5 - settings.DifferencePointForOwnGoal) && value - opponentValue >= settings.DifferencePointForOwnGoal)
                    goalTeam++;
                return goalTeam;
            }
        }
    }
}
