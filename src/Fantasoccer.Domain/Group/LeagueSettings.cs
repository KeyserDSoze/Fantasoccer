using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public enum LeagueCalendarType
    {
        Random,
        Cup
    }
    public enum FormationType
    {
        Normal,
        Best
    }
    public enum RoundType
    {
        Rank,
        Elimination
    }
    public sealed class LeagueRound
    {
        public string Name { get; set; }
        public RoundType Type { get; set; }
        public bool FromStart { get; set; }
        public int? FromRankingStartTeam { get; set; }
        public int? FromRankingEndTeam { get; set; }
    }
    public sealed class FromPreviousYearSettings
    {

    }
    public sealed class LeagueTypeSettings
    {
        public LeagueCalendarType CalendarType { get; set; }
        public List<LeagueRound> Rounds { get; set; }
        public LeagueTypeNumberSettings Numbers { get; set; }
    }
    public sealed class LeagueTypeNumberSettings
    {
        public int MaxPlayersInTeam { get; set; }
        public int GoalKeepersInTeam { get; set; }
        public int DefendersInTeam { get; set; }
        public int MidfieldersInTeam { get; set; }
        public int ForwardsInTeam { get; set; }
        public int GoalKeepersInBench { get; set; }
        public int DefendersInBench { get; set; }
        public int MidfieldersInBench { get; set; }
        public int ForwardsInBench { get; set; }
    }
    public class LeagueSetting
    {
        [JsonPropertyName("v")]
        public Dictionary<Role, VoteLeagueSetting> Votes { get; set; }
        //[JsonPropertyName("frm")]
        //public FormationType Formation { get; set; }
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
        [JsonPropertyName("gp")]
        public int PointForGoodPeople { get; set; }
        [JsonPropertyName("l")]
        public int PointForCleanSheet { get; set; }
        [JsonPropertyName("m")]
        public int MoneyForGoal { get; set; }
        [JsonPropertyName("n")]
        public int MoneyForSufferedGoal { get; set; }
        [JsonPropertyName("q")]
        public bool RandomAuction { get; set; }
        [JsonPropertyName("vp")]
        public bool RankWithValuePoints { get; set; }
        [JsonPropertyName("mk")]
        public MarketType Market { get; set; }
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
                 ManOfTheMatch = 2
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
            PointForStrongDefense = 2,
            PointForStrongDefense4 = 4,
            PointForStrongDefense5 = 6,
            MoneyForGoal = 5,
            MoneyForSufferedGoal = 3,
            PointForCleanSheet = 1,
            PointForGoodPeople = 2,
            RandomAuction = false
        };
    }
}
