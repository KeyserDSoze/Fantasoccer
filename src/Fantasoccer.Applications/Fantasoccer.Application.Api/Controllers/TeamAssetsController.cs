using Fantasoccer.Application.Core.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/team-assets")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class TeamAssetsController(TeamAssetsApplicationService assets) : ControllerBase
{
    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024 + 65536)]
    public async Task<TeamAssetResult> Upload(IFormFile image, [FromQuery] Guid groupId, [FromQuery] Guid basketId,
        [FromQuery] int year, [FromQuery] string owner, CancellationToken cancellationToken)
    {
        if (image.Length > 2 * 1024 * 1024) throw new Fantasoccer.Application.Core.Groups.ApplicationFault(400, "invalid_logo", "Dimensione massima 2 MiB.");
        using var stream = new MemoryStream();
        await image.CopyToAsync(stream, cancellationToken);
        return await assets.UploadAsync(User, groupId, basketId, year, owner, stream.ToArray(), cancellationToken);
    }
    [HttpPost("logo/copy-previous")]
    public Task<TeamAssetResult> Copy([FromQuery] Guid groupId, [FromQuery] Guid basketId, [FromQuery] int year,
        [FromQuery] string owner, CancellationToken cancellationToken) => assets.CopyPreviousAsync(User, groupId, basketId, year, owner, cancellationToken);
    public sealed record RenameTeamRequest(Guid GroupId, Guid BasketId, int Year, string Owner, string NewName);
    [HttpPatch("name")]
    public async Task<IActionResult> Rename(RenameTeamRequest request, CancellationToken cancellationToken)
    { await assets.RenameAsync(User, request.GroupId, request.BasketId, request.Year, request.Owner, request.NewName, cancellationToken); return NoContent(); }
}
