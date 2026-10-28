namespace Fantasoccer.Application.Core.Groups;

public sealed record GameGroupDto(Guid Id, string Name, GameLeagueDto[] Leagues, GameGroupUserDto[] Users, GameBasketDto[] Baskets, int Revision = 1);
public sealed record GameGroupUserDto(string Username, string Email, int Role);
public sealed record GameBasketDto(Guid Id, string Name, GameBasketYearDto[] Years, int Kind = 0, Guid? OriginBasketId = null);
public sealed record GameBasketYearDto(int Year, GameAnnualTeamDto[] Teams);
public sealed record GameAnnualTeamDto(string Name, string Owner, string[] AdditionalOwners, Guid? TeamId = null, string? LogoUrl = null, Guid? OriginTeamId = null);
public sealed record GameLeagueDto(Guid Id, string Name, bool IsMain, int Type, GameAnnualLeagueDto[] Years, Guid[] BasketsId, int Mode = 0);
// Sources: null keeps the stored derivations, [] clears them. Participants are read-only here.
public sealed record GameAnnualLeagueDto(int Year, int Type, LeagueSettingDto Settings, LeagueSourceDto[]? Sources = null, Guid[]? ParticipantTeamIds = null);
public sealed record VoteWeightDto(float Goal, float Penalty, float SufferedGoal, float StoppedPenalty, float WrongedPenalty,
    float OwnGoal, float Assist, float YellowCard, float RedCard, int Injury, int ManOfTheMatch);
public sealed record LeagueRoundDto(string? Name, int Type, bool FromStart, int? FromRankingStartTeam, int? FromRankingEndTeam);
public sealed record LeagueNumbersDto(int MaxPlayersInTeam, int MaxGoalKeepersInTeam, int MaxDefendersInTeam,
    int MaxMidfieldersInTeam, int MaxForwardsInTeam, int MaxGoalKeepersInBench, int MaxDefendersInBench, int MaxMidfieldersInBench, int MaxForwardsInBench);
public sealed record PreviousYearDto(Guid[] LeaguesId, int MaxTeamsPerLeague, int RoundType);
public sealed record CardTrainerDto(Dictionary<int, int> MaxCardsPerType);
public sealed record LeagueTypeSettingsDto(int CalendarType, LeagueRoundDto[] Rounds, LeagueNumbersDto Numbers,
    PreviousYearDto? FromPreviousYear, CardTrainerDto CardTrainer);
public sealed record OpeningPrizeDto(int Position, int Credits);
public sealed record OpeningSettingsDto(bool Enabled, int SerieADays, OpeningPrizeDto[] Prizes);
public sealed record PostponementSettingsDto(bool WaitForRecovery = false, float Goalkeeper = 6, float Defender = 6, float Midfielder = 6, float Forward = 6);

public sealed class LeagueSettingDto
{
    public Dictionary<int, VoteWeightDto> Votes { get; set; } = [];
    public int Formation { get; set; }
    public LeagueTypeSettingsDto? TypeSettings { get; set; }
    public int StartingMoney { get; set; }
    public int DelayedDay { get; set; }
    public int CancelledDay { get; set; }
    public int PointForFirstGoal { get; set; }
    public int PointForNextGoal { get; set; }
    public int PointForOwnGoal { get; set; }
    public int DifferencePointForOwnGoal { get; set; }
    public int PointInHome { get; set; }
    public int PointForVictory { get; set; }
    public int PointForDefeat { get; set; }
    public int PointForDraw { get; set; }
    public int PointForStrongDefense { get; set; }
    public int PointForStrongDefense4 { get; set; }
    public int PointForStrongDefense5 { get; set; }
    public int PointForGoodPeople { get; set; }
    public int PointForCleanSheet { get; set; }
    public int MoneyForGoal { get; set; }
    public int MoneyForSufferedGoal { get; set; }
    public bool RandomAuction { get; set; }
    public bool RankWithValuePoints { get; set; }
    public int Market { get; set; }
    public int LiveFormationChanges { get; set; }
    public bool AllowLiveModuleChange { get; set; }
    public OpeningSettingsDto OpeningCompetition { get; set; } = new(false, 2, []);
    public PostponementSettingsDto Postponements { get; set; } = new();
}
