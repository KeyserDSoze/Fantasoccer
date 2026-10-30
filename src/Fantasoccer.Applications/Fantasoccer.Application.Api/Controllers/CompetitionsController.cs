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
    LiveCompetitionApplicationService live, OpeningCompetitionApplicationService opening, PlayerOrderApplicationService playerOrder,
    CompetitionParticipationApplicationService participants, LeagueLineupApplicationService lineups) : ControllerBase
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
    public Task<RecalculationOutcome> Recalculate(Guid groupId, Guid leagueId, int year, RecalculateCompetitionRequest request, CancellationToken cancellationToken)
        => lifecycle.RecalculateAsync(User, groupId, leagueId, year, request, cancellationToken);
    [HttpGet("calendar")]
    public Task<FantasyCalendarDto?> Calendar(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => competitions.CalendarAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpGet("rank")]
    public Task<FantasyRankDto?> Rank(Guid groupId, Guid leagueId, int year, [FromQuery] int day, CancellationToken cancellationToken) => competitions.RankAsync(User, groupId, leagueId, year, day, cancellationToken);
    [HttpGet("games/{gameId:guid}")]
    public async Task<GameDto> Game(Guid groupId, Guid leagueId, int year, Guid gameId, [FromQuery] string editMode = "view", [FromQuery] bool asAdmin = false, CancellationToken cancellationToken = default)
        // Read-only views of the live day come from the shared group projection;
        // editing modes are always computed for the caller.
        => editMode == "view" && await live.TryGetGameAsync(User, groupId, leagueId, year, gameId, cancellationToken) is { } shared
            ? shared : await games.GetAsync(User, groupId, leagueId, year, gameId, editMode, asAdmin, cancellationToken);
    [HttpPut("games/{gameId:guid}/formation")]
    public Task<FormationSavedDto> Formation(Guid groupId, Guid leagueId, int year, Guid gameId, SaveGameFormationRequest request, CancellationToken cancellationToken)
        => games.SaveAsync(User, groupId, leagueId, year, gameId, request, cancellationToken);
    [HttpPost("games/{gameId:guid}/formation/copy")]
    public Task<FormationSavedDto> CopyFormation(Guid groupId, Guid leagueId, int year, Guid gameId, CopyGameFormationRequest request, CancellationToken cancellationToken)
        => games.CopyAsync(User, groupId, leagueId, year, gameId, request, cancellationToken);
    [HttpGet("lineups/copy-sources")]
    public Task<LineupCopySourceDto[]> CopySources(Guid groupId, Guid leagueId, int year, [FromQuery] int day, [FromQuery] Guid teamId, CancellationToken cancellationToken)
        => lineups.CopySourcesAsync(User, groupId, leagueId, year, day, teamId, cancellationToken);
    [HttpPost("lineups/copy")]
    public Task<FormationSavedDto> CopyLineup(Guid groupId, Guid leagueId, int year, CopyLineupRequest request, CancellationToken cancellationToken)
        => lineups.CopyAsync(User, groupId, leagueId, year, request, cancellationToken);
    [HttpGet("optimal-formation")]
    public Task<OptimalGameFormationDto> Optimal(Guid groupId, Guid leagueId, int year, [FromQuery] int day, [FromQuery] string? owner,
        [FromQuery] bool asAdmin, [FromQuery] Guid? teamId, CancellationToken cancellationToken) => games.OptimalAsync(User, groupId, leagueId, year, day, owner, asAdmin, cancellationToken, teamId);
    [HttpGet("participants")]
    public Task<LeagueParticipantsDto> Participants(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => participants.GetAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpPost("participants")]
    public async Task<IActionResult> Admit(Guid groupId, Guid leagueId, int year, AdmitParticipantRequest request, CancellationToken cancellationToken)
    { await participants.AdmitAsync(User, groupId, leagueId, year, request, cancellationToken); return NoContent(); }
    [HttpDelete("participants/{teamId:guid}")]
    public async Task<IActionResult> Withdraw(Guid groupId, Guid leagueId, int year, Guid teamId, CancellationToken cancellationToken)
    { await participants.WithdrawAsync(User, groupId, leagueId, year, teamId, cancellationToken); return NoContent(); }
    [HttpGet("qualification")]
    public Task<QualificationPreviewDto> Qualification(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => participants.PreviewAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpPost("qualification/confirm")]
    public Task<LeagueParticipantsDto> ConfirmQualification(Guid groupId, Guid leagueId, int year, ConfirmQualificationRequest request, CancellationToken cancellationToken)
        => participants.ConfirmAsync(User, groupId, leagueId, year, request, cancellationToken);
}
