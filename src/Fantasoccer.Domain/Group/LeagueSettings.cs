using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class LeagueSetting
    {
        [JsonPropertyName("v")]
        public Dictionary<Role, VoteLeagueSetting> Votes { get; set; }
        [JsonPropertyName("s")]
        public int StartingMoney { get; set; }
        [JsonPropertyName("d")]
        public int DelayedDay { get; set; }
        [JsonPropertyName("c")]
        public int CancelledDay { get; set; }
        [JsonPropertyName("g")]
        public int PointForFirstGoal { get; set; }
        [JsonPropertyName("t")]
        public int PointForNextGoal { get; set; }
        [JsonPropertyName("o")]
        public int PointForOwnGoal { get; set; }
        [JsonPropertyName("f")]
        public int DifferencePointForOwnGoal { get; set; }
        [JsonPropertyName("p")]
        public int PointInHome { get; set; }
        [JsonPropertyName("a")]
        public int PointForVictory { get; set; }
        [JsonPropertyName("b")]
        public int PointForDefeat { get; set; }
        [JsonPropertyName("h")]
        public int PointForDraw { get; set; }
        [JsonPropertyName("3")]
        public int PointForStrongDefense { get; set; }
        [JsonPropertyName("4")]
        public int PointForStrongDefense4 { get; set; }
        [JsonPropertyName("5")]
        public int PointForStrongDefense5 { get; set; }
        [JsonPropertyName("l")]
        public int PointForCleanSheet { get; set; }
        [JsonPropertyName("m")]
        public int MoneyForGoal { get; set; }
        [JsonPropertyName("n")]
        public int MoneyForSufferedGoal { get; set; }
        public static LeagueSetting Default => new()
        {
            StartingMoney = 1000,
            Votes = new Dictionary<Role, VoteLeagueSetting>() { { Role.Undefined, new VoteLeagueSetting()
            {
                 Assist = 1,
                 Goal = 3,
                 OwnGoal = -3,
                 Penalty = 3,
                 StoppedPenalty = 3,
                 RedCard = -1,
                 YellowCard=-0.5f,
                 SufferedGoal = -1,
                 WrongedPenalty = -3,
                 Injury = 0,
                 ManOfTheMatch = 0
            } } },
            DelayedDay = 2,
            CancelledDay = 1,
            PointForFirstGoal = 66,
            PointForNextGoal = 6,
            PointForOwnGoal = 6,
            DifferencePointForOwnGoal = 6,
            PointInHome = 0,
            PointForVictory = 3,
            PointForDefeat = 0,
            PointForDraw = 1,
            PointForStrongDefense = 3,
            PointForStrongDefense4 = 3,
            PointForStrongDefense5 = 3,
            MoneyForGoal = 5,
            MoneyForSufferedGoal = 3,
            PointForCleanSheet = 1,
        };
    }
}
