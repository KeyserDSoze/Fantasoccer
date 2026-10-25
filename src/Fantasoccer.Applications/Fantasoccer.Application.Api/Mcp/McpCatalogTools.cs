using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpCatalogTools(McpEndpointCatalog catalog, McpApiFacade facade)
{
    [McpServerTool(Name = "list_api_operations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Discover Fantasoccer controller/repository operations. Filter by controller/repository/action. Returns exact annotated input/response schemas, model IDs and the real repository key type for each factory. Use describe_api_model to decode compact aliases/enums; get_fantasoccer_guide explains workflows. year/y means season index, not calendar year. Discovery grants no permission. Start with get_my_account and get_current_season.")]
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
                readOnly = x.ReadOnly, description = x.Tool.Description, inputSchema = x.Tool.InputSchema,
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
                    encoding = "query.key is a string; for composite keys it contains a JSON object with the fields in keySchema."
                }
            })
        };
    }

    [McpServerTool(Name = "execute_api_operation", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Execute an operation discovered with list_api_operations. Supply its exact operation identifier and arguments matching its inputSchema (query, route, body, files). All existing API and business authorization checks apply. Writes and admin actions require an explicit user instruction. Do not retry writes automatically after an uncertain outcome. No arbitrary URL, credentials or impersonation headers are accepted.")]
    public Task<CallToolResult> ExecuteAsync(string operation, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
        => facade.CallAsync(new CallToolRequestParams { Name = operation, Arguments = arguments }, cancellationToken);
}
