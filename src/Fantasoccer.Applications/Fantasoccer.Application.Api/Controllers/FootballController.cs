using Fantasoccer.Application.Core.Football;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/football")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class FootballController(FootballApplicationService football, FootballIngestionApplicationService ingestion) : ControllerBase
{
    [HttpPost("refresh")] [Authorize(Policy = "super_admin")]
    public async Task<IActionResult> Refresh(RefreshFootballRequest request, CancellationToken cancellationToken)
    { await ingestion.RefreshAsync(User, request, cancellationToken); return NoContent(); }
    [HttpGet("years")] public Task<int[]> Years(CancellationToken cancellationToken) => football.YearsAsync(User, cancellationToken);
    [HttpGet("clubs")] public Task<ClubDto[]> Clubs(CancellationToken cancellationToken) => football.ClubsAsync(User, cancellationToken);
    [HttpGet("clubs/by-key/{clubKey}")]
    public Task<ClubHistoryDto?> ClubHistory(string clubKey, CancellationToken cancellationToken) => football.ClubHistoryAsync(User, clubKey, cancellationToken);
    [HttpGet("{year:int}/clubs")]
    public Task<ClubSeasonCatalogueDto?> SeasonClubs(int year, CancellationToken cancellationToken) => football.SeasonClubsAsync(User, year, cancellationToken);
    [HttpGet("players/by-key/{playerKey}")]
    public Task<PlayerHistoryDto?> PlayerHistory(string playerKey, CancellationToken cancellationToken) => football.PlayerHistoryAsync(User, playerKey, cancellationToken);
    [HttpGet("{year:int}/players")] public Task<PlayersDto<FootballPlayerDto>?> Players(int year, CancellationToken cancellationToken) => football.PlayersAsync(User, year, cancellationToken);
    [HttpGet("{year:int}/players/by-key/{playerKey}")]
    public Task<FootballPlayerDto?> PlayerByKey(int year, string playerKey, CancellationToken cancellationToken) => football.PlayerByKeyAsync(User, year, playerKey, cancellationToken);
    [HttpGet("{year:int}/calendar")] public Task<FootballCalendarDto?> Calendar(int year, CancellationToken cancellationToken) => football.CalendarAsync(User, year, cancellationToken);
    [HttpGet("{year:int}/rank")] public Task<ClubStandingsDto?> Rank(int year, CancellationToken cancellationToken) => football.RankAsync(User, year, cancellationToken);
    [HttpGet("{year:int}/statistics")] public Task<PlayersDto<StatisticPlayerDto>?> Statistics(int year, CancellationToken cancellationToken) => football.StatisticsAsync(User, year, cancellationToken);
    [HttpGet("{year:int}/days/{day:int}/votes/{source}")]
    public Task<PlayersDto<VotedPlayerDto>?> Votes(int year, int day, FootballVoteSource source, CancellationToken cancellationToken) => football.VotesAsync(User, year, day, source, cancellationToken);
    [HttpGet("{year:int}/days/{day:int}/chances")]
    public Task<PlayersDto<ChancedPlayerDto>?> Chances(int year, int day, CancellationToken cancellationToken) => football.ChancesAsync(User, year, day, cancellationToken);
    [HttpPatch("{year:int}/days/{day:int}/games/{ordinal:int}/delay")]
    [Authorize(Policy = "super_admin")]
    public async Task<IActionResult> Delay(int year, int day, int ordinal, DelayFootballGameRequest request, CancellationToken cancellationToken)
    { await football.SetDelayedAsync(User, year, day, ordinal, request, cancellationToken); return NoContent(); }
}
