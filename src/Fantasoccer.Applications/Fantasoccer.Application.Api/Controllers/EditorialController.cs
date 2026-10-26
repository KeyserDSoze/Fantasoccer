using Fantasoccer.Application.Core.Editorial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/groups/{groupId:guid}")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class EditorialController(EditorialApplicationService editorial) : ControllerBase
{
    [HttpGet("news/{year:int}/{day:int}")]
    public Task<NewsEditionDto?> News(Guid groupId, int year, int day, CancellationToken cancellationToken) => editorial.NewsAsync(User, groupId, year, day, cancellationToken);
    [HttpPost("news/{year:int}/{day:int}/{leagueId:guid}/generate")]
    public Task<NewsEditionDto> GenerateNews(Guid groupId, Guid leagueId, int year, int day, GenerateNewsRequest request, CancellationToken cancellationToken) => editorial.GenerateNewsAsync(User, groupId, leagueId, year, day, request, cancellationToken);
    [HttpGet("teams/{teamId:guid}/advice/{year:int}/{day:int}")]
    public Task<TeamAdviceDto?> Advice(Guid groupId, Guid teamId, int year, int day, CancellationToken cancellationToken) => editorial.AdviceAsync(User, groupId, teamId, year, day, cancellationToken);
    [HttpPost("teams/{teamId:guid}/advice/{year:int}/{day:int}/generate")]
    public Task<TeamAdviceDto> GenerateAdvice(Guid groupId, Guid teamId, int year, int day, GenerateAdviceRequest request, CancellationToken cancellationToken) => editorial.GenerateAdviceAsync(User, groupId, teamId, year, day, request, cancellationToken);
}
