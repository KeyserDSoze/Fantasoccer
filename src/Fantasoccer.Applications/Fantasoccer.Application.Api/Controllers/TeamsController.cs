using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/teams")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class TeamsController(TeamRosterApplicationService teams, TeamCreditsApplicationService credits, TeamRosterAvailabilityService availability,
    Fantasoccer.Application.Core.Competitions.LeagueLineupApplicationService lineups) : ControllerBase
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
    // Shared lineups across the competitions of a team (default) or one lineup per competition.
    [HttpGet("lineup-settings")]
    public Task<Fantasoccer.Application.Core.Competitions.TeamLineupSettingsDto> LineupSettings([FromQuery] Guid groupId, [FromQuery] int year, [FromQuery] Guid teamId, CancellationToken cancellationToken)
        => lineups.SettingsAsync(User, groupId, year, teamId, cancellationToken);
    [HttpPut("lineup-settings")]
    public Task<Fantasoccer.Application.Core.Competitions.TeamLineupSettingsDto> SetLineupSettings(Fantasoccer.Application.Core.Competitions.SetTeamLineupSettingsRequest request, CancellationToken cancellationToken)
        => lineups.SetSettingsAsync(User, request, cancellationToken);
    [HttpPut("formation")]
    public Task<FormationSavedDto> Save(SaveRosterFormationRequest request, CancellationToken cancellationToken) => request.Kind == (int)RosterKind.Matchday
        ? lineups.SaveRosterRequestAsync(User, request, cancellationToken) : teams.SaveFormationAsync(User, request, cancellationToken);
}
