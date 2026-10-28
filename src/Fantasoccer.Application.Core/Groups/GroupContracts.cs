namespace Fantasoccer.Application.Core.Groups;

public sealed record AccountSummaryDto(Guid UserId, string Email, string DisplayName, bool CanCreateGroups);
public sealed record GroupSummaryDto(Guid GroupId, string Name, int Roles);
public sealed record GroupMemberDto(Guid UserId, string? Email, string DisplayName, int Roles)
{
    public bool IsActive => Roles != 0;
}
public sealed record GroupMemberLookupDto(string Email, string? DisplayName, bool Found, bool IsMember);
public sealed record BasketDto(Guid BasketId, string Name, int[] SeasonIndices, int Kind = 0, Guid? OriginBasketId = null);
public sealed record LeagueDto(Guid LeagueId, string Name, bool IsMain, LeagueSeasonDto[] Seasons, int Mode = 0);
public sealed record LeagueSeasonDto(int SeasonIndex, Guid[] BasketIds, LeagueSourceDto[]? Sources = null, Guid[]? ParticipantTeamIds = null);
public sealed record LeagueSourceDto(Guid SourceLeagueId, int QualifiedTeams, string? Stage = null);
public sealed record TeamOwnerDto(Guid UserId, string DisplayName, bool IsPrimary);
// Owners of a derived team are the effective owners of its origin team.
public sealed record TeamSummaryDto(Guid TeamId, Guid BasketId, int SeasonIndex, string Name, TeamOwnerDto[] Owners, Guid? OriginTeamId = null);
public sealed record GroupDetailsDto(Guid GroupId, string Name, int Roles, GroupMemberDto[] Members,
    BasketDto[] Baskets, LeagueDto[] Leagues, TeamSummaryDto[] Teams);
public sealed record CreateGroupRequest(string Name);
public sealed record RenameGroupRequest(string Name);
public sealed record SetGroupMemberRequest(string Email, string DisplayName, int Roles);
public sealed record UpdateGroupMemberRequest(bool IsActive, int Roles, string? Email = null);
public sealed record CreateBasketRequest(string Name, int SeasonIndex, Guid? OriginBasketId = null);
public sealed record CreateLeagueRequest(string Name, int SeasonIndex, Guid[] BasketIds, int Mode = 0, LeagueSourceDto[]? Sources = null);
// A derived team (OriginTeamId set) has no owners of its own: OwnerUserId must be empty.
public sealed record CreateTeamRequest(Guid BasketId, int SeasonIndex, string Name, Guid OwnerUserId, Guid[] AdditionalOwnerUserIds, Guid? OriginTeamId = null);
public sealed record BasketCreatedDto(Guid BasketId);
public sealed record LeagueCreatedDto(Guid LeagueId);
public sealed record TeamCreatedDto(Guid TeamId);

public sealed class ApplicationFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
