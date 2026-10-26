using Fantasoccer.Application.Core.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/groups")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class GroupsController(GameGroupApplicationService groups) : ControllerBase
{
    [HttpGet] public Task<GameGroupDto[]> List(CancellationToken cancellationToken) => groups.ListAsync(User, cancellationToken);
    [HttpGet("{groupId:guid}")] public Task<GameGroupDto> Get(Guid groupId, CancellationToken cancellationToken) => groups.GetAsync(User, groupId, cancellationToken);
    [HttpPut("{groupId:guid}")] public Task<GameGroupDto> Update(Guid groupId, GameGroupDto request, CancellationToken cancellationToken) => groups.UpdateAsync(User, groupId, request, cancellationToken);
}
