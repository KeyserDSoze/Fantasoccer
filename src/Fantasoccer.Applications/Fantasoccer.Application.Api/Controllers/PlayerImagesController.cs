using Fantasoccer.Application.Core.Football;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/player-images")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class PlayerImagesController(PlayerImagesApplicationService images) : ControllerBase
{
    [HttpPost("refresh")]
    public Task<RefreshedPlayerImageDto> Refresh(RefreshPlayerImageRequest request, CancellationToken cancellationToken) => images.RefreshAsync(User, request, cancellationToken);
    [HttpPost("refresh-team")]
    public Task<TeamImagesRefreshedDto> Team(RefreshTeamImagesRequest request, CancellationToken cancellationToken) => images.RefreshTeamAsync(User, request, cancellationToken);
}
