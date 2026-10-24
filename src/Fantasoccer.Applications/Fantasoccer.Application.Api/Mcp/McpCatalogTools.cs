using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpCatalogTools(McpEndpointCatalog catalog, McpApiFacade facade)
{
    [McpServerTool(Name = "list_api_operations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Discover all existing Fantasoccer controller and repository operations, including admin operations. Filter by controller/repository name or action. Returns operation identifiers and their exact input schemas. Discovery does not grant permission to execute. Start with get_my_account to discover your own group IDs.")]
    public object ListOperations(string? filter = null, int offset = 0, int limit = 10)
    {
        var operations = catalog.Operations.Values.Where(x => string.IsNullOrEmpty(filter) ||
            x.Api.RelativePath!.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Tool.Name).ToArray();
        return new
        {
            total = operations.Length,
            items = operations.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 20)).Select(x => new
            {
                operation = x.Tool.Name, method = x.Api.HttpMethod, path = x.Api.RelativePath,
                readOnly = x.ReadOnly, description = x.Tool.Description, inputSchema = x.Tool.InputSchema
            })
        };
    }

    [McpServerTool(Name = "execute_api_operation", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Execute an operation discovered with list_api_operations. Supply its exact operation identifier and arguments matching its inputSchema (query, route, body, files). All existing API and business authorization checks apply. Writes and admin actions require an explicit user instruction. Do not retry writes automatically after an uncertain outcome. No arbitrary URL, credentials or impersonation headers are accepted.")]
    public Task<CallToolResult> ExecuteAsync(string operation, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        => facade.CallAsync(new CallToolRequestParams { Name = operation, Arguments = arguments }, cancellationToken);
}
