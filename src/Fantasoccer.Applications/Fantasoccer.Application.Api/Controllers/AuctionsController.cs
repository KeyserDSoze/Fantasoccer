using Fantasoccer.Application.Core.Auctions;
using Fantasoccer.Application.Core.Football;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/auctions")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class AuctionsController(AuctionApplicationService auctions) : ControllerBase
{
    [HttpPost] public Task<Guid> Create(CreateAuctionRequest request, CancellationToken cancellationToken) => auctions.CreateAsync(User, request, cancellationToken);
    [HttpGet("{auctionId:guid}")] public Task<AuctionStateDto> Get(Guid auctionId, CancellationToken cancellationToken) => auctions.GetAsync(User, auctionId, cancellationToken);
    [HttpGet("{auctionId:guid}/discarded")]
    public Task<StatisticPlayerDto[]> Discarded(Guid auctionId, [FromQuery] int role, CancellationToken cancellationToken) => auctions.DiscardedAsync(User, auctionId, role, cancellationToken);
    [HttpGet("{auctionId:guid}/presence")] public Task<string[]> Presence(Guid auctionId, CancellationToken cancellationToken) => auctions.ConnectedUsersAsync(User, auctionId, cancellationToken);
    [HttpPost("{auctionId:guid}/commands/{operation}")]
    public Task<AuctionUpdateDto> Command(Guid auctionId, string operation, AuctionCommandRequest request, CancellationToken cancellationToken) => auctions.CommandAsync(User, auctionId, operation, request, cancellationToken);
}
