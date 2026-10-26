namespace Fantasoccer.Application.Core.Groups;

public sealed record AccountSummaryDto(Guid UserId, string Email, string DisplayName, bool CanCreateGroups);
public sealed record GroupSummaryDto(Guid GroupId, string Name, int Roles);
public sealed record GroupMemberDto(Guid UserId, string? Email, string DisplayName, int Roles)
{
    public bool IsActive => Roles != 0;
}
public sealed record GroupMemberLookupDto(string Email, string? DisplayName, bool Found, bool IsMember);
public sealed record BasketDto(Guid BasketId, string Name, int[] SeasonIndices);
public sealed record LeagueDto(Guid LeagueId, string Name, bool IsMain, LeagueSeasonDto[] Seasons);
public sealed record LeagueSeasonDto(int SeasonIndex, Guid[] BasketIds);
public sealed record TeamOwnerDto(Guid UserId, string DisplayName, bool IsPrimary);
public sealed record TeamSummaryDto(Guid TeamId, Guid BasketId, int SeasonIndex, string Name, TeamOwnerDto[] Owners);
public sealed record GroupDetailsDto(Guid GroupId, string Name, int Roles, GroupMemberDto[] Members,
    BasketDto[] Baskets, LeagueDto[] Leagues, TeamSummaryDto[] Teams);
public sealed record CreateGroupRequest(string Name);
public sealed record RenameGroupRequest(string Name);
public sealed record SetGroupMemberRequest(string Email, string DisplayName, int Roles);
public sealed record UpdateGroupMemberRequest(bool IsActive, int Roles, string? Email = null);
public sealed record CreateBasketRequest(string Name, int SeasonIndex);
public sealed record CreateLeagueRequest(string Name, int SeasonIndex, Guid[] BasketIds);
public sealed record CreateTeamRequest(Guid BasketId, int SeasonIndex, string Name, Guid OwnerUserId, Guid[] AdditionalOwnerUserIds);
public sealed record BasketCreatedDto(Guid BasketId);
public sealed record LeagueCreatedDto(Guid LeagueId);
public sealed record TeamCreatedDto(Guid TeamId);

public sealed class ApplicationFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
