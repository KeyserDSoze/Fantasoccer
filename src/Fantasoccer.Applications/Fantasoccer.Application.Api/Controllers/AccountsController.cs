using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "authenticated")]
[Route("api/accounts")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class AccountsController(AccountApplicationService accounts) : ControllerBase
{
    [HttpGet("me")] public Task<AccountIdentityDto> Me(CancellationToken cancellationToken) => accounts.CurrentAsync(User, cancellationToken);
    [HttpGet] public Task<AccountIdentityDto[]> Search([FromQuery] string? email, CancellationToken cancellationToken) => accounts.SearchAsync(User, email, cancellationToken);
    [HttpPatch("me/profile")] public async Task<IActionResult> Profile(UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    { await accounts.UpdateProfileAsync(User, request, cancellationToken); return NoContent(); }
    [HttpPut("me/selection")] public async Task<IActionResult> Selection(AccountStateDto request, CancellationToken cancellationToken)
    { await accounts.UpdateSelectionAsync(User, request, cancellationToken); return NoContent(); }
    [HttpGet("me/settings")] public Task<UserSettingsDto> Settings(CancellationToken cancellationToken) => accounts.SettingsAsync(User, cancellationToken);
    [HttpPut("me/settings")] public async Task<IActionResult> Settings(UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    { await accounts.UpdateSettingsAsync(User, request, cancellationToken); return NoContent(); }
    [HttpPost] public async Task<IActionResult> Provision(ProvisionAccountRequest request, CancellationToken cancellationToken)
    { await accounts.ProvisionAsync(User, request, cancellationToken); return NoContent(); }
    [HttpDelete("{email}")] [Authorize(Policy = "super_admin")]
    public async Task<IActionResult> Disable(string email, CancellationToken cancellationToken)
    { await accounts.DisableAsync(User, email, cancellationToken); return NoContent(); }
}
