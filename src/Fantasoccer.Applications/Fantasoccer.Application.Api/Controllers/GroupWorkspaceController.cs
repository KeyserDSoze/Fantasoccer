using Fantasoccer.Application.Core.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/workspace")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class GroupWorkspaceController(GroupApplicationService groups, GroupCreationApplicationService creation) : ControllerBase
{
    [HttpGet("account")]
    public Task<AccountSummaryDto> Account(CancellationToken cancellationToken) => groups.GetAccountAsync(User, cancellationToken);

    [HttpGet("groups")]
    public Task<GroupSummaryDto[]> List(CancellationToken cancellationToken) => groups.ListAsync(User, cancellationToken);

    [HttpGet("groups/{groupId:guid}")]
    public Task<GroupDetailsDto> Get(Guid groupId, CancellationToken cancellationToken) => groups.GetAsync(User, groupId, cancellationToken);

    [HttpPost("groups")]
    [Authorize(Policy = "super_admin")]
    public Task<GroupSummaryDto> Create(CreateGroupRequest request, CancellationToken cancellationToken) => groups.CreateAsync(User, request, cancellationToken);

    [HttpPost("groups/redeem-code")]
    public Task<GroupSummaryDto> Redeem(RedeemGroupCodeRequest request, CancellationToken cancellationToken) => creation.RedeemAsync(User, request, cancellationToken);

    [HttpPatch("groups/{groupId:guid}")]
    public async Task<IActionResult> Rename(Guid groupId, RenameGroupRequest request, CancellationToken cancellationToken)
    { await groups.RenameAsync(User, groupId, request, cancellationToken); return NoContent(); }

    [HttpPut("groups/{groupId:guid}/members")]
    public async Task<IActionResult> SetMember(Guid groupId, SetGroupMemberRequest request, CancellationToken cancellationToken)
    { await groups.SetMemberAsync(User, groupId, request, cancellationToken); return NoContent(); }

    [HttpPatch("groups/{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid groupId, Guid userId, UpdateGroupMemberRequest request, CancellationToken cancellationToken)
    { await groups.UpdateMemberAsync(User, groupId, userId, request, cancellationToken); return NoContent(); }

    [HttpGet("groups/{groupId:guid}/members/lookup")]
    public Task<GroupMemberLookupDto> LookupMember(Guid groupId, [FromQuery] string? email, CancellationToken cancellationToken)
        => groups.LookupMemberAsync(User, groupId, email, cancellationToken);

    [HttpPost("groups/{groupId:guid}/baskets")]
    public async Task<BasketCreatedDto> CreateBasket(Guid groupId, CreateBasketRequest request, CancellationToken cancellationToken)
        => new(await groups.CreateBasketAsync(User, groupId, request, cancellationToken));

    [HttpPost("groups/{groupId:guid}/leagues")]
    public async Task<LeagueCreatedDto> CreateLeague(Guid groupId, CreateLeagueRequest request, CancellationToken cancellationToken)
        => new(await groups.CreateLeagueAsync(User, groupId, request, cancellationToken));

    [HttpPost("groups/{groupId:guid}/teams")]
    public async Task<TeamCreatedDto> CreateTeam(Guid groupId, CreateTeamRequest request, CancellationToken cancellationToken)
        => new(await groups.CreateTeamAsync(User, groupId, request, cancellationToken));
}

public sealed class WorkspaceFaultFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApplicationFault fault) return;
        var problem = new ProblemDetails { Status = fault.Status, Title = fault.Code, Detail = fault.Message };
        problem.Extensions["code"] = fault.Code;
        context.Result = new ObjectResult(problem) { StatusCode = fault.Status };
        context.ExceptionHandled = true;
    }
}
