namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class LeagueRules
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int Formation { get; set; }
    public int StartingMoney { get; set; } = 1000;
    public int DelayedDay { get; set; } = 2;
    public int CancelledDay { get; set; } = 1;
    public int PointForFirstGoal { get; set; } = 66;
    public int PointForNextGoal { get; set; } = 6;
    public int PointForOwnGoal { get; set; } = 6;
    public int DifferencePointForOwnGoal { get; set; } = 6;
    public int PointInHome { get; set; }
    public int PointForVictory { get; set; } = 3;
    public int PointForDefeat { get; set; }
    public int PointForDraw { get; set; } = 1;
    public int PointForStrongDefense { get; set; } = 2;
    public int PointForStrongDefense4 { get; set; } = 4;
    public int PointForStrongDefense5 { get; set; } = 6;
    public int PointForGoodPeople { get; set; } = 2;
    public int PointForCleanSheet { get; set; } = 1;
    public int MoneyForGoal { get; set; } = 5;
    public int MoneyForSufferedGoal { get; set; } = 3;
    public bool RandomAuction { get; set; }
    public bool RankWithValuePoints { get; set; }
    public int Market { get; set; }
    public int LiveFormationChanges { get; set; }
    public bool AllowLiveModuleChange { get; set; }
    public bool WaitForPostponedMatches { get; set; }
    public float PoliticalGoalkeeper { get; set; } = 6;
    public float PoliticalDefender { get; set; } = 6;
    public float PoliticalMidfielder { get; set; } = 6;
    public float PoliticalForward { get; set; } = 6;
    public bool OpeningCompetitionEnabled { get; set; }
    public int OpeningCompetitionSerieADays { get; set; } = 2;
    public bool HasTypeSettings { get; set; }
    public int CalendarType { get; set; }
    public int MaxPlayersInTeam { get; set; } = 25;
    public int MaxGoalKeepersInTeam { get; set; } = 1;
    public int MaxDefendersInTeam { get; set; } = 5;
    public int MaxMidfieldersInTeam { get; set; } = 4;
    public int MaxForwardsInTeam { get; set; } = 2;
    public int MaxGoalKeepersInBench { get; set; } = 1;
    public int MaxDefendersInBench { get; set; } = 1;
    public int MaxMidfieldersInBench { get; set; } = 1;
    public int MaxForwardsInBench { get; set; } = 1;
    public bool HasPreviousYearSettings { get; set; }
    public int PreviousYearMaxTeamsPerLeague { get; set; }
    public int PreviousYearRoundType { get; set; }
    public ICollection<LeagueVoteWeight> Votes { get; set; } = [];
    public ICollection<LeagueStageRule> Rounds { get; set; } = [];
    public ICollection<LeagueCardLimit> CardLimits { get; set; } = [];
    public ICollection<LeagueOpeningPrize> OpeningPrizes { get; set; } = [];
    public ICollection<PreviousLeagueQualification> PreviousLeagues { get; set; } = [];
}

public sealed class LeagueVoteWeight
{
    public int PlayerRole { get; set; }
    public float Goal { get; set; }
    public float Penalty { get; set; }
    public float SufferedGoal { get; set; }
    public float StoppedPenalty { get; set; }
    public float WrongedPenalty { get; set; }
    public float OwnGoal { get; set; }
    public float Assist { get; set; }
    public float YellowCard { get; set; }
    public float RedCard { get; set; }
    public int Injury { get; set; }
    public int ManOfTheMatch { get; set; }
}

public sealed class LeagueStageRule
{
    public int Ordinal { get; set; }
    public string? Name { get; set; }
    public int Type { get; set; }
    public bool FromStart { get; set; }
    public int? FromRankingStartTeam { get; set; }
    public int? FromRankingEndTeam { get; set; }
}

public sealed class LeagueCardLimit { public int CardType { get; set; } public int Maximum { get; set; } }
public sealed class LeagueOpeningPrize { public int Position { get; set; } public int Credits { get; set; } }
public sealed class PreviousLeagueQualification { public Guid SourceLeagueId { get; set; } }
