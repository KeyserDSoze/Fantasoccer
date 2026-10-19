using System.Security.Claims;
using Fantasoccer.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPush;

namespace Fantasoccer.Application.Api.Controllers;

[Authorize(Policy = "authenticated")]
[ApiController]
[Route("api/push-notifications")]
public sealed class PushNotificationController(PushNotificationSettingsService settings) : ControllerBase
{
    private string Email => User.FindFirstValue(ClaimTypes.Email)!;
    private bool IsOwnRequest(string? email) => email == null || string.Equals(email, Email, StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<ActionResult<PushNotificationSettings>> Get([FromQuery] string? email, CancellationToken cancellationToken)
        => !IsOwnRequest(email) ? Forbid() : Ok(await settings.GetAsync(Email, cancellationToken));

    [HttpPatch]
    public async Task<ActionResult<PushNotificationSettings>> Update([FromBody] PushNotificationPreferences patch, [FromQuery] string? email, CancellationToken cancellationToken)
        => !IsOwnRequest(email) ? Forbid() : Ok(await settings.UpdateAsync(Email, patch, cancellationToken));

    [HttpPost("devices")]
    public async Task<ActionResult<PushNotificationSettings>> AddDevice([FromBody] RegisterPushDevice request, [FromQuery] string? email, CancellationToken cancellationToken)
    {
        if (!IsOwnRequest(email)) return Forbid();
        if (!PushSubscriptionValidator.IsValid(request.Subscription)) return BadRequest(new { error = "Subscription push non valida." });
        return Ok(await settings.AddDeviceAsync(Email, request.Subscription!, request.PreviousEndpoint, cancellationToken));
    }

    [HttpPost("devices/remove")]
    public async Task<ActionResult<PushNotificationSettings>> RemoveDevice([FromBody] RemovePushDevice request, [FromQuery] string? email, CancellationToken cancellationToken)
    {
        if (!IsOwnRequest(email)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Endpoint)) return BadRequest();
        return Ok(await settings.RemoveDeviceAsync(Email, request.Endpoint, cancellationToken));
    }

    [HttpPost("disable")]
    public async Task<ActionResult<PushNotificationSettings>> Disable([FromQuery] string? email, CancellationToken cancellationToken)
        => !IsOwnRequest(email) ? Forbid() : Ok(await settings.DisableAllAsync(Email, cancellationToken));

}

public sealed record RegisterPushDevice(PushSubscription? Subscription, string? PreviousEndpoint);
public sealed record RemovePushDevice(string Endpoint);
