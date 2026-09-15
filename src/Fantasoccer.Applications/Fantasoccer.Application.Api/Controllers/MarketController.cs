using Fantasoccer.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[Authorize(Policy = "authenticated")]
[ApiController]
[Route("[controller]/[action]")]
public sealed class MarketController : ControllerBase
{
    private readonly IMarketManager _marketManager;
    private readonly IMarketNotificationService _marketNotificationService;

    public MarketController(IMarketManager marketManager, IMarketNotificationService marketNotificationService)
    {
        _marketManager = marketManager;
        _marketNotificationService = marketNotificationService;
    }

    [HttpGet]
    public async Task<ActionResult<MarketWrapper>> GetAsync(
        [FromQuery] string groupId,
        [FromQuery] int year,
        [FromQuery] string leagueId,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user))
            return Unauthorized();
        var result = await _marketManager.GetAsync(
            new LeagueKey { Group = groupId, League = leagueId, Year = year },
            user,
            cancellationToken);
        return result == null ? Forbid() : Ok(result);
    }

    [HttpPost]
    public async ValueTask<MarketOperationResult> CreateAsync(
        [FromQuery] string groupId,
        [FromQuery] int year,
        [FromQuery] string leagueId,
        [FromBody] Market proposal,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user))
            return new MarketOperationResult { Message = "Utente non autenticato." };
        var key = new LeagueKey { Group = groupId, League = leagueId, Year = year };
        var result = await _marketManager.CreateAsync(key, proposal, user, cancellationToken);
        if (result.IsOk && result.Market != null)
        {
            if (result.Market.Status == MarketStatus.Pending)
                await _marketNotificationService.NotifyProposalCreatedAsync(key, result.Market, cancellationToken);
            else
                await _marketNotificationService.NotifyOutcomeAsync(key, result.Market, cancellationToken);
        }
        return result;
    }

    [HttpPost]
    public async ValueTask<MarketOperationResult> ApproveAsync(
        [FromQuery] string groupId,
        [FromQuery] int year,
        [FromQuery] string leagueId,
        [FromQuery] string marketId,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user))
            return new MarketOperationResult { Message = "Utente non autenticato." };
        var key = new LeagueKey { Group = groupId, League = leagueId, Year = year };
        var result = await _marketManager.ApproveAsync(key, marketId, user, cancellationToken);
        if (result.IsOk && result.Market != null)
            await _marketNotificationService.NotifyOutcomeAsync(key, result.Market, cancellationToken);
        return result;
    }

    [HttpPost]
    public async ValueTask<MarketOperationResult> DenyAsync(
        [FromQuery] string groupId,
        [FromQuery] int year,
        [FromQuery] string leagueId,
        [FromQuery] string marketId,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user))
            return new MarketOperationResult { Message = "Utente non autenticato." };
        var key = new LeagueKey { Group = groupId, League = leagueId, Year = year };
        var result = await _marketManager.DenyAsync(key, marketId, user, cancellationToken);
        if (result.IsOk && result.Market != null)
            await _marketNotificationService.NotifyOutcomeAsync(key, result.Market, cancellationToken);
        return result;
    }

    [HttpPost]
    public ValueTask<MarketOperationResult> CancelAsync(
        [FromQuery] string groupId,
        [FromQuery] int year,
        [FromQuery] string leagueId,
        [FromQuery] string marketId,
        CancellationToken cancellationToken)
        => ExecuteAsync((key, user) => _marketManager.CancelAsync(key, marketId, user, cancellationToken), groupId, year, leagueId);

    private ValueTask<MarketOperationResult> ExecuteAsync(
        Func<LeagueKey, string, ValueTask<MarketOperationResult>> action,
        string groupId,
        int year,
        string leagueId)
    {
        var user = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(user)
            ? ValueTask.FromResult(new MarketOperationResult { Message = "Utente non autenticato." })
            : action(new LeagueKey { Group = groupId, League = leagueId, Year = year }, user);
    }
}
