using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "super_admin")]
[Route("api/platform")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformController(PlatformApplicationService platform, GroupCreationApplicationService creation) : ControllerBase
{
    [HttpGet("users")]
    public Task<PlatformPage<PlatformUserDto>> Users([FromQuery] string? query, [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => platform.UsersAsync(User, query, offset, limit, cancellationToken);
    [HttpGet("groups")]
    public Task<PlatformPage<PlatformGroupDto>> Groups([FromQuery] string? query, [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => platform.GroupsAsync(User, query, offset, limit, cancellationToken);
    [HttpGet("groups/{id:guid}")]
    public Task<PlatformGroupDetailsDto> Group(Guid id, CancellationToken cancellationToken) => platform.GroupAsync(User, id, cancellationToken);
    [HttpGet("group-codes")]
    public Task<PlatformPage<GroupCodeDto>> Codes([FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => platform.CodesAsync(User, offset, limit, cancellationToken);
    [HttpPost("group-codes")]
    public Task<IssuedGroupCodeDto> Issue(IssueGroupCodeRequest request, CancellationToken cancellationToken) => creation.IssueAsync(User, request, cancellationToken);
    [HttpPost("group-codes/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    { await creation.RevokeAsync(User, id, cancellationToken); return NoContent(); }
}
