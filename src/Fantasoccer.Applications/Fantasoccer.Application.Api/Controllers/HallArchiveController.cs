using Fantasoccer.Application.Core.HallOfFame;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/groups/{groupId:guid}/leagues/{leagueId:guid}/hall-of-fame")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class HallArchiveController(HallOfFameApplicationService hall, HallRefreshApplicationService refresh) : ControllerBase
{
    [HttpGet] public Task<HallOfFameDto?> Get(Guid groupId, Guid leagueId, CancellationToken cancellationToken) => hall.GetAsync(User, groupId, leagueId, cancellationToken);
    [HttpPost("refresh")] public async Task<IActionResult> Refresh(Guid groupId, Guid leagueId, RefreshHallRequest request, CancellationToken cancellationToken)
    { await refresh.RefreshAsync(User, groupId, leagueId, request, cancellationToken); return NoContent(); }
    [HttpPost("confirm-baseline")] public async Task<IActionResult> ConfirmBaseline(Guid groupId, Guid leagueId, RefreshHallRequest request, CancellationToken cancellationToken)
    { await refresh.ConfirmBaselineAsync(User, groupId, leagueId, request, cancellationToken); return NoContent(); }
}
