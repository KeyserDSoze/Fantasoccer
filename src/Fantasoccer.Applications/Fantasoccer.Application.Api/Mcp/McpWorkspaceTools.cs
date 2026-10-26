using System.ComponentModel;
using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpWorkspaceTools(GroupApplicationService groups, IHttpContextAccessor accessor)
{
    private ClaimsPrincipal Caller => accessor.HttpContext?.User ?? new();

    [McpServerTool(Name = "list_my_relational_groups", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List the caller's PostgreSQL groups using readable DTO fields, not legacy Blob documents. Membership is checked by the shared application service.")]
    public Task<GroupSummaryDto[]> List(CancellationToken cancellationToken) => Invoke(() => groups.ListAsync(Caller, cancellationToken));

    [McpServerTool(Name = "get_relational_group", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read members, baskets, league participation and team ownership from PostgreSQL. This workspace does not yet include rosters, fixtures or scores.")]
    public Task<GroupDetailsDto> Get(Guid groupId, CancellationToken cancellationToken) => Invoke(() => groups.GetAsync(Caller, groupId, cancellationToken));

    [McpServerTool(Name = "create_relational_group", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create a PostgreSQL group. Only an authenticated global administrator can create groups; the application service assigns the first group owner.")]
    public Task<GroupSummaryDto> Create(string name, CancellationToken cancellationToken) => Invoke(() => groups.CreateAsync(Caller, new(name), cancellationToken));

    private static async Task<T> Invoke<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (ApplicationFault fault) { throw new McpException(fault.Code + ": " + fault.Message); }
    }
}
