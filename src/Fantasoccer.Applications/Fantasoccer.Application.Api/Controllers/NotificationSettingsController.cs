using Fantasoccer.Application.Core.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/push-notifications")]
[RequestSizeLimit(16384)]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class NotificationSettingsController(NotificationSettingsApplicationService settings, PushOutboxApplicationService outbox) : ControllerBase
{
    [HttpGet("quarantine")] [Authorize(Policy = "super_admin")]
    public Task<QuarantinedPushDto[]> Quarantine(CancellationToken cancellationToken) => outbox.QuarantineAsync(User, cancellationToken);
    [HttpPost("quarantine/{deliveryId:guid}/confirm-scope")] [Authorize(Policy = "super_admin")]
    public async Task<IActionResult> ConfirmScope(Guid deliveryId, ConfirmPushScopeRequest request, CancellationToken cancellationToken)
    { await outbox.ConfirmScopeAsync(User, deliveryId, request, cancellationToken); return NoContent(); }
    [HttpGet] public Task<PushSettingsDto> Get([FromQuery] string? email, CancellationToken cancellationToken) => settings.GetAsync(User, email, cancellationToken);
    [HttpPatch] public Task<PushSettingsDto> Patch(PushPreferencesPatch request, [FromQuery] string? email, CancellationToken cancellationToken) => settings.PatchAsync(User, request, email, cancellationToken);
    [HttpPost("devices")] public Task<RegisteredNotificationDeviceDto> Register(RegisterNotificationDeviceRequest request, [FromQuery] string? email, CancellationToken cancellationToken) => settings.RegisterAsync(User, request, email, cancellationToken);
    [HttpPost("devices/remove")] public Task<PushSettingsDto> Remove(RemoveNotificationDeviceRequest request, [FromQuery] string? email, CancellationToken cancellationToken) => settings.RemoveAsync(User, request.Endpoint, email, cancellationToken);
    [HttpPost("disable")] public Task<PushSettingsDto> Disable([FromQuery] string? email, CancellationToken cancellationToken) => settings.DisableAsync(User, email, cancellationToken);
}
