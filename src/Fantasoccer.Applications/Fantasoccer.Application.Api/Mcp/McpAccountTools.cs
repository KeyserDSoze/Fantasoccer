using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpAccountTools(IHttpContextAccessor context, IRepository<AppIdentity, string> users)
{
    [McpServerTool(Name = "get_my_account", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get your authenticated Fantasoccer account, current selections and group identifiers. Start here to discover the keys for the repository/controller tools. Identity is resolved exclusively by the server.")]
    public async Task<object> GetMyAccountAsync(CancellationToken cancellationToken)
    {
        var email = context.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException();
        var account = await users.GetAsync(email, cancellationToken) ?? throw new UnauthorizedAccessException();
        return new { account.Email, account.Username, account.Groups, account.State };
    }
}
