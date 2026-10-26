using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record PointDto(float Value, bool DefensiveBonus, bool GoodPeople, bool OwnGoal);
public sealed record ResultDto(PointDto Home, PointDto Away, bool IsCancelled, int HomeGoals, int AwayGoals);
public sealed record CalendarGameDto(Guid Id, int Number, string Home, string? HomeOwner, string Away, string? AwayOwner,
    ResultDto? Result, Guid? HomeTeamId, Guid? AwayTeamId);
public sealed record CalendarDayDto(int SerieADay, int Number, CalendarGameDto[] Games);
public sealed record FantasyCalendarDto(int Year, Dictionary<string, CalendarDayDto[]> Rounds, int Version);
public sealed record RankedTeamDto(Guid TeamId, Guid BasketId, string Name, string Owner, float Point, int Victories, int Draws, int Defeats,
    int Goal, int SufferedGoal, float ValuePoint, float SufferedValuePoint, int PlusMoney, int Money)
{
    public int ValueAssets => Money + PlusMoney;
}
public sealed record FantasyRankDto(int SerieADay, Dictionary<string, RankedTeamDto[]> Rounds, int SnapshotDay);
public sealed record FinalVoteDto(float Value, bool Special);
public sealed record LivePlayerDto(RosterPlayerDto Current, VoteDto? Vote, ChanceDto? Chance, FootballGameDto? RealGame, FinalVoteDto? FinalValue, int CurrentPosition);
public sealed record LiveTeamDto(Guid TeamId, Guid BasketId, int Version, int? SourceVersion, string Name, string Owner, string[] AdditionalOwners,
    LivePlayerDto[] Players, string? LastUpdate, int FormationChangesMaximum, int FormationChangesUsed, int FormationChangesRemaining,
    FormationChangeDto[] FormationChangeHistory, string? LogoUrl, bool CanEdit);
public sealed record GameDto(LiveTeamDto[] Teams, CalendarGameDto Game, int SerieADay, bool CanEdit, bool IsLiveFormationWindow,
    DateTimeOffset? LiveFormationDeadline, int? LiveFormationChangesRemaining, bool ResultIsPreview);
public sealed record SaveGameFormationRequest(Guid TeamId, FormationPositionRequest[] Positions, int ExpectedVersion,
    int? SourceVersion = null, bool Live = false, bool AsAdmin = false);
public sealed record OptimalGamePlayerDto(Guid PlayerId, string PlayerKey, string PlayerName, int Role, int Position, float Projection, float Confidence);
public sealed record OptimalGameFormationDto(OptimalGamePlayerDto[] Players, string Module, float Score, float Confidence, string[] Warnings);
