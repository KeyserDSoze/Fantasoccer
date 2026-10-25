using Fantasoccer.Application.Api.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("Context/[action]")]
public sealed class ContextController(FantasoccerSeasonContext seasons) : ControllerBase
{
    [HttpGet]
    public FantasoccerSeasonSnapshot GetCurrentSeason() => seasons.GetCurrent();
}
