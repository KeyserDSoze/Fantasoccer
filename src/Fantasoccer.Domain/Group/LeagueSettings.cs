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
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("t")]
        public RoundType Type { get; set; }
        [JsonPropertyName("f")]
        public bool FromStart { get; set; }
        [JsonPropertyName("s")]
        public int? FromRankingStartTeam { get; set; }
        [JsonPropertyName("e")]
        public int? FromRankingEndTeam { get; set; }
    }
    public sealed class FromPreviousYearSettings
    {
        /// <summary>
        /// Gets or sets the list of league identifiers associated with the entity.
        /// </summary>
        [JsonPropertyName("l")]
        public List<string> LeaguesId { get; set; } = [];
        /// <summary>
        /// Gets or sets the maximum number of teams allowed in a league.
        /// </summary>
        [JsonPropertyName("m")]
        public int MaxTeamsPerLeague { get; set; }
        /// <summary>
        /// Getting the teams from previous year by Rank or by Elimination, for instance the first 4 teams in Rank or the semifinalists in Elimination, based on MaxTeamsPerLeague
        /// </summary>
        [JsonPropertyName("t")]
        public RoundType RoundType { get; set; }
    }
    public sealed class CardTrainerSettings
    {
        [JsonPropertyName("c")]
        public Dictionary<CardType, int> MaxCardsPerType { get; set; } = [];
    }
    public sealed class OpeningCompetitionPrize
    {
        [JsonPropertyName("p")]
        public int Position { get; set; }
        [JsonPropertyName("c")]
        public int Credits { get; set; }
    }
    public sealed class OpeningCompetitionSettings
    {
        [JsonPropertyName("e")]
        public bool Enabled { get; set; }
        [JsonPropertyName("d")]
        public int SerieADays { get; set; }
        [JsonPropertyName("p")]
        public List<OpeningCompetitionPrize> Prizes { get; set; } = [];
        public static OpeningCompetitionSettings Default => new()
        {
            Enabled = false,
            SerieADays = 2,
            Prizes =
            [
                new() { Position = 1, Credits = 50 },
                new() { Position = 2, Credits = 30 },
                new() { Position = 3, Credits = 20 }
            ]
        };
    }
    public sealed class LeagueTypeSettings
    {
        [JsonPropertyName("t")]
        public LeagueCalendarType CalendarType { get; set; }
        [JsonPropertyName("r")]
        public List<LeagueRound> Rounds { get; set; } = [];
        [JsonPropertyName("n")]
        public LeagueTypeNumberSettings Numbers { get; set; } = LeagueTypeNumberSettings.Default;
        [JsonPropertyName("fpy")]
        public FromPreviousYearSettings? FromPreviousYear { get; set; }
        [JsonPropertyName("ct")]
        public CardTrainerSettings CardTrainer { get; set; } = new CardTrainerSettings();
        public static LeagueTypeSettings Default { get; } = new LeagueTypeSettings()
        {
            CalendarType = LeagueCalendarType.Random,
            Rounds =
            [
                new()
                {
                    Name = "@",
                    Type = RoundType.Rank,
                    FromStart = true
                }
            ],
            Numbers = LeagueTypeNumberSettings.Default,
            FromPreviousYear = null
        };
    }
    public sealed class LeagueTypeNumberSettings
    {
        [JsonPropertyName("t")]
        public int MaxPlayersInTeam { get; set; }
        [JsonPropertyName("g")]
        public int MaxGoalKeepersInTeam { get; set; }
        [JsonPropertyName("d")]
        public int MaxDefendersInTeam { get; set; }
        [JsonPropertyName("m")]
        public int MaxMidfieldersInTeam { get; set; }
        [JsonPropertyName("f")]
        public int MaxForwardsInTeam { get; set; }
        [JsonPropertyName("mg")]
        public int MaxGoalKeepersInBench { get; set; }
        [JsonPropertyName("md")]
        public int MaxDefendersInBench { get; set; }
        [JsonPropertyName("mb")]
        public int MaxMidfieldersInBench { get; set; }
        [JsonPropertyName("fb")]
        public int MaxForwardsInBench { get; set; }
        public static LeagueTypeNumberSettings Default { get; } = new LeagueTypeNumberSettings()
        {
            MaxPlayersInTeam = 25,
            MaxGoalKeepersInTeam = 1,
            MaxDefendersInTeam = 5,
            MaxMidfieldersInTeam = 4,
            MaxForwardsInTeam = 2,
            MaxGoalKeepersInBench = 1,
            MaxDefendersInBench = 1,
            MaxMidfieldersInBench = 1,
            MaxForwardsInBench = 1
        };
    }
    public class LeagueSetting
    {
        [JsonPropertyName("v")]
        public Dictionary<Role, VoteLeagueSetting> Votes { get; set; }
        [JsonPropertyName("frm")]
        public FormationType Formation { get; set; }
        [JsonPropertyName("lt")]
        public LeagueTypeSettings? Type { get; set; }
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
        [JsonPropertyName("lch")]
        public int LiveFormationChanges { get; set; }
        [JsonPropertyName("lcm")]
        public bool AllowLiveModuleChange { get; set; }
        [JsonPropertyName("oc")]
        public OpeningCompetitionSettings OpeningCompetition { get; set; } = OpeningCompetitionSettings.Default;
        public static LeagueSetting Default => new()
        {
            StartingMoney = 1000,
            LiveFormationChanges = 0,
            AllowLiveModuleChange = false,
            OpeningCompetition = OpeningCompetitionSettings.Default,
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
