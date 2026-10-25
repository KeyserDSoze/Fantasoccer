using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpCatalogTools(McpEndpointCatalog catalog, McpApiFacade facade)
{
    [McpServerTool(Name = "list_api_operations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Discover supported Fantasoccer operations and point-read recipes. This searches operation metadata, not user documents. Generic repository Query/Count/Operation scans and aggregations are not exposed to MCP. Resolve IDs from get_my_account, Group/Get and the calendar, then use Get/Exist with the exact key. Returns annotated schemas, key models and pointReadRecipe when available. Use get_fantasoccer_guide(topic='lookups') for navigation; year/y means season index. Discovery grants no permission.")]
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
                pointReadRecipe = McpPointReadRecipes.Find(x.Api.RelativePath!),
                inputModels = x.Api.ParameterDescriptions.Where(p => p.Source == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
                    .SelectMany(p => McpModelCatalog.ReferencedTypes(p.Type)).Select(McpJsonContracts.ModelId).Distinct(),
                responseSchemas = x.Api.SupportedResponseTypes.Select(response => new
                {
                    statusCode = response.StatusCode,
                    models = response.Type == null ? [] : McpModelCatalog.ReferencedTypes(response.Type).Select(McpJsonContracts.ModelId).ToArray(),
                    schema = response.Type == null || response.Type == typeof(void) ? null : McpJsonContracts.Schema(response.Type),
                    note = response.Type == null || response.Type == typeof(object) || response.Type == typeof(void)
                        ? "Response shape not fully declared by this endpoint; consult the workflow guide/command discovery. Do not guess compact fields."
                        : "Declared API response body, inside the facade's JSON-string body wrapper. Check business success even on HTTP 200."
                }),
                repository = x.Repository == null ? null : new
                {
                    model = McpJsonContracts.ModelId(x.Repository.ModelType),
                    keyType = McpJsonContracts.ModelId(x.Repository.KeyType), factory = x.Repository.Factory,
                    keySchema = McpJsonContracts.Schema(x.Repository.KeyType),
                    encoding = "For MCP, query.key is a string containing a JSON object with all fields in keySchema. The server validates/canonicalizes it for authorization and converts it with Rystem KeySettings before dispatch; do not construct a native separator-based string. Simple keys remain unchanged."
                }
            })
        };
    }

    [McpServerTool(Name = "execute_api_operation", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Execute an operation discovered with list_api_operations. Supply its exact identifier and arguments matching its inputSchema (query, route, body, files). query is the HTTP parameter container, NOT a generic document search. For reads resolve IDs from account/group/calendar and use Get/Exist; repository scans/aggregations are unavailable even with old cached identifiers. Existing authorization checks apply. Writes require explicit user instruction; do not retry uncertain writes. No arbitrary URL, credentials or impersonation headers.")]
    public Task<CallToolResult> ExecuteAsync(string operation, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        => facade.CallAsync(new CallToolRequestParams { Name = operation, Arguments = arguments }, cancellationToken);
}
