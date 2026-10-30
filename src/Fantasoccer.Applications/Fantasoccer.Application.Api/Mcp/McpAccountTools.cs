using System.ComponentModel;
using System.Security.Claims;
using Fantasoccer.Application.Core.Accounts;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpAccountTools(IHttpContextAccessor context, FantasoccerSeasonContext seasons, AccountApplicationService accounts)
{
    [McpServerTool(Name = "get_my_account", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Start here: get the authenticated account, memberships, saved selections and current season. Readable fields and UUIDs usable with /api/groups. Saved selections are not permissions.")]
    public async Task<object> GetMyAccountAsync(CancellationToken cancellationToken)
    {
        var user = context.HttpContext?.User;
        if (user?.FindFirstValue(ClaimTypes.Email) is null) throw new UnauthorizedAccessException();
        var profile = await accounts.CurrentAsync(user, cancellationToken);
        return new { profile.Id, profile.Email, profile.Username, profile.Groups, profile.State,
            currentSeason = seasons.GetCurrent().CurrentSeason,
            nextRead = new { path = "api/workspace/groups", note = "Use readable groupId values; selection does not grant authorization." } };
    }
}
