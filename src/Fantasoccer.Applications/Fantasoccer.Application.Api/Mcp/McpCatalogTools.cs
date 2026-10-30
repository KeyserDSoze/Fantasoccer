using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpCatalogTools(McpEndpointCatalog catalog, McpApiFacade facade)
{
    [McpServerTool(Name = "list_api_operations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Discover the supported Fantasoccer operations. This searches operation metadata, not user data. Resolve IDs from get_my_account and the group/competition reads, then call the operation with execute_api_operation. Returns the annotated input schema and the declared response models. Use get_fantasoccer_guide(topic='lookups') for navigation; year means season index. Discovery grants no permission.")]
    public object ListOperations(string? filter = null, int offset = 0, int limit = 10)
    {
        var operations = catalog.McpOperations.Values.Where(x => string.IsNullOrEmpty(filter) ||
            x.Api.RelativePath!.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Tool.Name).ToArray();
        return new
        {
            total = operations.Length,
            items = operations.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 20)).Select(x => new
            {
                operation = x.Tool.Name, method = x.Api.HttpMethod, path = x.Api.RelativePath,
                readOnly = x.ReadOnly, description = x.Tool.Description, inputSchema = x.Tool.InputSchema,
                inputModels = x.Api.ParameterDescriptions.Where(p => p.Source == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
                    .SelectMany(p => McpModelCatalog.ReferencedTypes(p.Type)).Select(McpJsonContracts.ModelId).Distinct(),
                responseSchemas = x.Api.SupportedResponseTypes.Select(response => new
                {
                    statusCode = response.StatusCode,
                    models = response.Type == null ? [] : McpModelCatalog.ReferencedTypes(response.Type).Select(McpJsonContracts.ModelId).ToArray(),
                    schema = response.Type == null || response.Type == typeof(void) ? null : McpJsonContracts.Schema(response.Type),
                    note = response.Type == null || response.Type == typeof(object) || response.Type == typeof(void)
                        ? "Response shape not fully declared by this endpoint; read the endpoint through list_api_operations and do not guess the shape."
                        : "Declared API response body, inside the facade's JSON-string body wrapper. Check business success even on HTTP 200."
                })
            })
        };
    }

    [McpServerTool(Name = "execute_api_operation", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Execute an operation discovered with list_api_operations. Supply its exact identifier and arguments matching its inputSchema (query, route, body, files). Resolve IDs from the account, group and calendar reads first. The application's authorization checks apply. Writes require explicit user instruction; do not retry uncertain writes. No arbitrary URL, credentials or impersonation headers.")]
    public Task<CallToolResult> ExecuteAsync(string operation, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        => facade.CallAsync(new CallToolRequestParams { Name = operation, Arguments = arguments }, cancellationToken);
}
