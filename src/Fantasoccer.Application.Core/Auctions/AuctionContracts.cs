using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Core.Auctions;

public sealed record AuctionScopeDto(Guid Group, Guid League, int Year);
public sealed record AuctionTeamKeyDto(Guid Group, int Year, Guid BasketId, string Email);
public sealed record AuctionTeamDto(AuctionTeamKeyDto Key, TeamRosterDto Value);
public sealed record AuctionRoleCountDto(int Total, int Shown);
public sealed record AuctionWinDto(string PlayerId, string PlayerKey, Guid? TeamId, string? Owner, int Price);
public sealed record ActualAuctionDto(StatisticPlayerDto Player, DateTimeOffset Started, DateTimeOffset? TimerStartedAtUtc, Guid? WinningTeamId,
    string? Owner, string? SubstitutedPlayerId, string? Substituted, int Price);
public sealed record AuctionWrapperDto(Guid Id, AuctionScopeDto LeagueKey, string Creator, DateTimeOffset Created, int Type, int Kind,
    ActualAuctionDto? Actual, AuctionWinDto[] Winnings, int Status, Dictionary<int, AuctionRoleCountDto> PlayerCounts,
    GameAnnualTeamDto[] Participants, Dictionary<int, string?> LastShow, int SecondsPerAuction, int CurrentRole, int Version);
public sealed record AuctionStateDto(AuctionWrapperDto Wrapper, DateTimeOffset LastUpdate, GameAnnualLeagueDto League, string LeagueName,
    AuctionTeamDto[] Teams, StatisticPlayerDto[] RealPlayers, bool IsAuctioneer, string UserEmail);
public sealed record CreateAuctionRequest(Guid GroupId, Guid LeagueId, int Year, int Type, int Kind);
public sealed record AuctionCommandRequest(int ExpectedVersion, int? Role = null, string? PlayerId = null, Guid? TeamId = null,
    int? Amount = null, string? SubstitutedPlayerId = null, int? Timer = null, Guid? NewAuctioneerUserId = null,
    int? PlayerStatus = null, int? Revenue = null, int? RosterOrdinal = null, int? ExpectedRosterVersion = null);
public sealed record AuctionUpdateDto(Guid AuctionId, int Version, string Operation);
public interface IAuctionUpdatePublisher
{
    Task PublishAsync(AuctionUpdateDto update, CancellationToken cancellationToken);
}
