using Fantasoccer.Application.Core.Market;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/competitions/{groupId:guid}/{leagueId:guid}/{year:int}/market")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class TradesController(MarketApplicationService market) : ControllerBase
{
    [HttpGet] public Task<MarketDto[]> List(Guid groupId, Guid leagueId, int year, CancellationToken cancellationToken) => market.ListAsync(User, groupId, leagueId, year, cancellationToken);
    [HttpPost] public Task<MarketOperationDto> Create(Guid groupId, Guid leagueId, int year, CreateMarketRequest request, CancellationToken cancellationToken) => market.CreateAsync(User, groupId, leagueId, year, request, cancellationToken);
    [HttpPost("{tradeId:guid}/{operation:regex(^(accept|reject|approve|deny|cancel)$)}")]
    public Task<MarketOperationDto> Command(Guid groupId, Guid leagueId, int year, Guid tradeId, string operation, MarketCommandRequest request, CancellationToken cancellationToken) => market.CommandAsync(User, groupId, leagueId, year, tradeId, operation, request, cancellationToken);
}
