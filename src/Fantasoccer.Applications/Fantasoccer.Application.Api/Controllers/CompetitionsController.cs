using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/competitions/{groupId:guid}/{leagueId:guid}/{year:int}")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class CompetitionsController(CompetitionApplicationService competitions, GameApplicationService games, CompetitionLifecycleApplicationService lifecycle,
    LiveCompetitionApplicationService live, OpeningCompetitionApplicationService opening, PlayerOrderApplicationService playerOrder) : ControllerBase
{
    [HttpGet("auction-order")]
    public Task<PlayerOrderDto?> AuctionOrder(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => playerOrder.GetAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpPost("auction-order/randomize")]
    public async Task<IActionResult> RandomizeAuctionOrder(Guid groupId, Guid leagueId, int year, RandomizePlayerOrderRequest request, CancellationToken cancellationToken)
    { await playerOrder.RandomizeAsync(User, groupId, leagueId, year, request, cancellationToken); return NoContent(); }
    [HttpGet("opening/standings")]
    public Task<OpeningStandingsDto?> OpeningStandings(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => opening.GetAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpPost("opening/prepare")]
    public Task<OpeningStandingsDto> PrepareOpening(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => opening.PrepareAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpGet("opening/team")]
    public Task<TeamRosterDto> OpeningTeam(Guid groupId, Guid leagueId, int year, [FromQuery] int day = 1, [FromQuery] Guid? teamId = null, CancellationToken cancellationToken = default)
        => opening.TeamAsync(User, groupId, leagueId, year, day, teamId, cancellationToken);
    [HttpGet("/api/groups/{groupId:guid}/live")]
    public Task<LiveGroupDto> Live(Guid groupId, [FromQuery] int? year, CancellationToken cancellationToken) => live.GetAsync(User, groupId, year ?? FantacalcioTime.ActualYear, cancellationToken);
    [HttpPost("calendar/generate")]
    public async Task<IActionResult> Generate(Guid groupId, Guid leagueId, int year, GenerateCompetitionRequest request, CancellationToken cancellationToken)
    { await lifecycle.GenerateAsync(User, groupId, leagueId, year, request, cancellationToken); return NoContent(); }
    [HttpPost("recalculate")]
    public async Task<IActionResult> Recalculate(Guid groupId, Guid leagueId, int year, RecalculateCompetitionRequest request, CancellationToken cancellationToken)
    { await lifecycle.RecalculateAsync(User, groupId, leagueId, year, request, cancellationToken); return NoContent(); }
    [HttpGet("calendar")]
    public Task<FantasyCalendarDto?> Calendar(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => competitions.CalendarAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpGet("rank")]
    public Task<FantasyRankDto?> Rank(Guid groupId, Guid leagueId, int year, [FromQuery] int day, CancellationToken cancellationToken) => competitions.RankAsync(User, groupId, leagueId, year, day, cancellationToken);
    [HttpGet("games/{gameId:guid}")]
    public Task<GameDto> Game(Guid groupId, Guid leagueId, int year, Guid gameId, [FromQuery] string editMode = "view", [FromQuery] bool asAdmin = false, CancellationToken cancellationToken = default)
        => games.GetAsync(User, groupId, leagueId, year, gameId, editMode, asAdmin, cancellationToken);
    [HttpPut("games/{gameId:guid}/formation")]
    public Task<FormationSavedDto> Formation(Guid groupId, Guid leagueId, int year, Guid gameId, SaveGameFormationRequest request, CancellationToken cancellationToken)
        => games.SaveAsync(User, groupId, leagueId, year, gameId, request, cancellationToken);
    [HttpGet("optimal-formation")]
    public Task<OptimalGameFormationDto> Optimal(Guid groupId, Guid leagueId, int year, [FromQuery] int day, [FromQuery] string owner,
        [FromQuery] bool asAdmin, CancellationToken cancellationToken) => games.OptimalAsync(User, groupId, leagueId, year, day, owner, asAdmin, cancellationToken);
}
