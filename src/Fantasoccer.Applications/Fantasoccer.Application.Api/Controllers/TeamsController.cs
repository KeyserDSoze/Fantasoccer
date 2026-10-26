using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/teams")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class TeamsController(TeamRosterApplicationService teams, TeamCreditsApplicationService credits, TeamRosterAvailabilityService availability) : ControllerBase
{
    [HttpPost("availability/synchronize")]
    public async Task<IActionResult> Synchronize(SynchronizeRosterAvailabilityRequest request, CancellationToken cancellationToken)
    { await availability.SynchronizeAsync(User, request, cancellationToken); return NoContent(); }
    [HttpPost("credits/confirm-rank-checkpoint")]
    public async Task<IActionResult> ConfirmCredits(ConfirmRankCreditsRequest request, CancellationToken cancellationToken)
    { await credits.ConfirmAsync(User, request, cancellationToken); return NoContent(); }
    [HttpGet("roster")]
    public Task<TeamRosterDto?> Get([FromQuery] Guid groupId, [FromQuery] Guid basketId, [FromQuery] int year, [FromQuery] string owner,
        [FromQuery] int kind, [FromQuery] int day, [FromQuery] Guid? leagueId, [FromQuery] Guid? teamId, CancellationToken cancellationToken)
        => teams.GetAsync(User, groupId, basketId, year, owner, (RosterKind)kind, day, leagueId, cancellationToken, teamId);
    [HttpPut("formation")]
    public Task<FormationSavedDto> Save(SaveRosterFormationRequest request, CancellationToken cancellationToken) => teams.SaveFormationAsync(User, request, cancellationToken);
}
