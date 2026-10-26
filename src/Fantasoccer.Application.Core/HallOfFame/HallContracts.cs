using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Core.HallOfFame;

public sealed record HallRecordGameDto(CalendarGameDto Game, int Year, bool IsLinkedToCalendar);
public sealed record HallRecordPlayerDto(RosterPlayerDto Player, float Points);
public sealed record HallSeasonPlayerDto(RosterPlayerDto Player, int Year, float Points);
public sealed record HallWinningPlayerDto(RosterPlayerDto Player, Dictionary<string, int[]> Wins);
public sealed record HallWinningTeamDto(Guid OwnerUserId, string Owner, string? TeamName, Dictionary<string, int[]> Wins);
public sealed record HallRankDto(Guid OwnerUserId, string Name, string Owner, float Points, int Victories, int Draws, int Defeats,
    int Goals, int SufferedGoals, float ValuePoints, float SufferedValuePoints, int PlusMoney, int Money)
{
    public int ValueAssets => Money + PlusMoney;
}
public sealed record HallOfFameDto(HallRecordGameDto? RecordGame, HallRecordPlayerDto? RecordPlayer, HallSeasonPlayerDto? PlayerWithMostPointsInYear,
    HallWinningPlayerDto[] WinningPlayers, HallWinningTeamDto[] WinningTeams, HallRankDto[] AllTimeRankings, int Version, bool BaselineConfirmed = false);
