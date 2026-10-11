using System.Text.Json;
using System.Threading.Concurrent;
using System.Runtime.ExceptionServices;
using Fantasoccer.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers;

[Authorize(Policy = "authenticated")]
[ApiController]
[Route("[controller]/[action]")]
public sealed class PlayerImageController(
    PlayerImageService images,
    IRepository<AppIdentity, string> users,
    IRepository<RealPlayersWrapper, int> players,
    ILock imageLock,
    ILogger<PlayerImageController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReloadAsync([FromBody] ReloadPlayerImage request)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var identity = User.Identity?.Name;
        if (identity == null) return Unauthorized();
        var user = await users.GetAsync(identity, cancellationToken);
        if (user?.Groups.Any(x => string.Equals(x.Id, request.GroupId, StringComparison.OrdinalIgnoreCase)) != true)
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.PlayerKey) || request.Year <= 0)
            return BadRequest(new { message = "Giocatore o stagione non validi." });
        var wrapper = await players.GetAsync(request.Year, cancellationToken);
        var player = wrapper?.Players?.FirstOrDefault(x => string.Equals(x.Key, request.PlayerKey, StringComparison.OrdinalIgnoreCase));
        if (player == null) return NotFound(new { message = "Giocatore non trovato nella stagione selezionata." });
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var updated = false;
            Exception? failure = null;
            var completed = false;
            await imageLock.ExecuteAsync(async () =>
            {
                // The lock runner can swallow callback exceptions; preserve them for the API response.
                try
                {
                    updated = await images.ReloadAsync(player, timeout.Token);
                    completed = true;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }, $"player-image:{player.Key}");
            if (failure != null) ExceptionDispatchInfo.Throw(failure);
            if (!completed) return StatusCode(503, new { message = "Aggiornamento già in corso. Riprova tra poco." });
            if (!updated) return NotFound(new { message = "Nessuna foto ufficiale disponibile. L'immagine attuale non è stata modificata." });
            return Ok(new { playerKey = player.Key, revision = Guid.NewGuid().ToString("N") });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "La fonte immagini sta impiegando troppo tempo. Riprova più tardi." });
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException)
        {
            logger.LogWarning(exception, "Unable to reload image for player {PlayerKey}", player.Key);
            return StatusCode(502, new { message = "La fonte immagini non è disponibile o ha restituito una foto non valida. Riprova più tardi." });
        }
    }
}

public sealed class ReloadPlayerImage
{
    public required string GroupId { get; set; }
    public required string PlayerKey { get; set; }
    public int Year { get; set; }
}
