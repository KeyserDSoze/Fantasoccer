using Fantasoccer.Application.Core.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/cards")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class CardsController(CardApplicationService cards) : ControllerBase
{
    [HttpGet] public Task<CardDto[]> List(CancellationToken cancellationToken) => cards.ListAsync(User, cancellationToken);
    [HttpGet("{cardId:guid}")] public Task<CardDto> Get(Guid cardId, CancellationToken cancellationToken) => cards.GetAsync(User, cardId, cancellationToken);
    [HttpPost] [Authorize(Policy = "super_admin")]
    public Task<CardDto> Create(WriteCardRequest request, CancellationToken cancellationToken) => cards.CreateAsync(User, request, cancellationToken);
    [HttpPut("{cardId:guid}")] [Authorize(Policy = "super_admin")]
    public Task<CardDto> Update(Guid cardId, WriteCardRequest request, CancellationToken cancellationToken) => cards.UpdateAsync(User, cardId, request, cancellationToken);
    [HttpDelete("{cardId:guid}")] [Authorize(Policy = "super_admin")]
    public async Task<IActionResult> Delete(Guid cardId, [FromQuery] int expectedVersion, CancellationToken cancellationToken)
    { await cards.DeleteAsync(User, cardId, expectedVersion, cancellationToken); return NoContent(); }
    [HttpPost("{cardId:guid}/image")] [Authorize(Policy = "super_admin")]
    [Consumes("multipart/form-data")] [RequestSizeLimit(5 * 1024 * 1024 + 65536)]
    public async Task<CardDto> Image(Guid cardId, IFormFile file, [FromQuery] int expectedVersion, CancellationToken cancellationToken)
    {
        if (file.Length > 5 * 1024 * 1024) throw new Fantasoccer.Application.Core.Groups.ApplicationFault(400, "image_too_large", "Dimensione massima 5 MiB.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, cancellationToken);
        return await cards.UploadImageAsync(User, cardId, expectedVersion, stream.ToArray(), cancellationToken);
    }
}
