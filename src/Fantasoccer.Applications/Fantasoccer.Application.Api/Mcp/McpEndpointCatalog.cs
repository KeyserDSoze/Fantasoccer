using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ModelContextProtocol.Protocol;

namespace Fantasoccer.Application.Api.Mcp;

public sealed record McpOperation(ApiDescription Api, Tool Tool, bool ReadOnly);

// The API operations an MCP client may discover and execute. Each one runs the real endpoint in process, so every
// application authorization check applies; this catalog only decides what is advertised.
public sealed class McpEndpointCatalog
{
    // Platform administration is deliberately not exposed to MCP.
    private static readonly HashSet<string> ExposedControllers = new(StringComparer.Ordinal)
    {
        "Accounts", "Groups", "Teams", "TeamAssets", "Football", "Competitions", "Trades", "Auctions", "HallArchive", "Cards",
        "NotificationSettings", "PlayerImages", "Editorial", "Operations", "GroupWorkspace", "Context", "Ping"
    };

    private readonly Lazy<IReadOnlyDictionary<string, McpOperation>> _operations;

    public McpEndpointCatalog(IApiDescriptionGroupCollectionProvider descriptions) => _operations = new(() => Build(descriptions));

    public IReadOnlyDictionary<string, McpOperation> McpOperations => _operations.Value;

    private static IReadOnlyDictionary<string, McpOperation> Build(IApiDescriptionGroupCollectionProvider descriptions)
    {
        var result = new Dictionary<string, McpOperation>(StringComparer.Ordinal);
        foreach (var api in descriptions.ApiDescriptionGroups.Items.SelectMany(x => x.Items))
        {
            var path = api.RelativePath?.Split('?')[0];
            if (path == null || api.HttpMethod == null) continue;
            // Only the application's own controllers, never auth, Swagger, MCP or arbitrary URLs.
            if (api.ActionDescriptor is not ControllerActionDescriptor { ControllerName: var controller } || !ExposedControllers.Contains(controller)) continue;

            var queryProperties = new JsonObject();
            var routeProperties = new JsonObject();
            var queryRequired = new JsonArray();
            var routeRequired = new JsonArray();
            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var parameter in api.ParameterDescriptions)
            {
                if (parameter.Source == BindingSource.Services || parameter.Type == typeof(CancellationToken)) continue;
                if (parameter.Source == BindingSource.Body)
                {
                    properties["body"] = Schema(parameter.Type, "properties/body");
                    if (parameter.IsRequired) required.Add("body");
                }
                else if (parameter.Source == BindingSource.FormFile || parameter.Type == typeof(IFormFile))
                {
                    var files = properties["files"] as JsonObject ?? new JsonObject();
                    files[parameter.Name] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["fileName"] = new JsonObject { ["type"] = "string" },
                            ["contentType"] = new JsonObject { ["type"] = "string" },
                            ["base64"] = new JsonObject { ["type"] = "string", ["description"] = "File bytes, base64 encoded. Maximum 2 MiB." }
                        },
                        ["required"] = new JsonArray("fileName", "contentType", "base64"), ["additionalProperties"] = false
                    };
                    properties["files"] = files.DeepClone();
                    if (!required.Any(x => x?.ToString() == "files")) required.Add("files");
                }
                else
                {
                    var isRoute = parameter.Source == BindingSource.Path;
                    var schema = Schema(parameter.Type,
                        $"properties/{(isRoute ? "route" : "query")}/properties/{parameter.Name.Replace("~", "~0").Replace("/", "~1")}");
                    if (schema is JsonObject obj)
                    {
                        if (McpJsonContracts.ParameterDescription(parameter.Name) is { } meaning) obj["description"] = meaning;
                    }
                    (isRoute ? routeProperties : queryProperties)[parameter.Name] = schema;
                    if (parameter.IsRequired || isRoute) (isRoute ? routeRequired : queryRequired).Add(parameter.Name);
                }
            }
            if (queryProperties.Count > 0)
            {
                properties["query"] = ObjectSchema(queryProperties, queryRequired);
                if (queryRequired.Count > 0) required.Add("query");
            }
            if (routeProperties.Count > 0)
            {
                properties["route"] = ObjectSchema(routeProperties, routeRequired);
                required.Add("route");
            }
            var key = api.HttpMethod + " " + path;
            var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8].ToLowerInvariant();
            var label = new string((api.HttpMethod + "_" + path).Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray());
            var name = label[..Math.Min(54, label.Length)] + "_" + suffix;
            var readOnly = IsReadOnly(api.HttpMethod);
            var tool = new Tool
            {
                Name = name,
                Description = $"{api.HttpMethod} /{path}. Executes the existing Fantasoccer API with your authenticated account and all API/business authorization checks. " +
                    (readOnly ? "Reads data." : "MAY CHANGE DATA. Obtain the user's explicit instruction before calling; do not retry automatically after an uncertain outcome.") +
                    " year means season index (15=2026/27), not calendar year; ids of groups, leagues, baskets, teams and games are UUIDs. Use describe_api_model for the meaning of the fields. No credentials or user impersonation headers are accepted.",
                InputSchema = JsonSerializer.SerializeToElement(ObjectSchema(properties, required)),
                Annotations = new ToolAnnotations { ReadOnlyHint = readOnly, DestructiveHint = !readOnly, IdempotentHint = readOnly, OpenWorldHint = false }
            };
            result.TryAdd(name, new(api, tool, readOnly));
        }
        return result;
    }

    // The relational controllers never write on GET.
    private static bool IsReadOnly(string method) => method is "GET" or "HEAD";

    private static JsonObject ObjectSchema(JsonObject properties, JsonArray required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false
    };

    private static JsonNode Schema(Type type, string location)
    {
        try
        {
            var schema = McpJsonContracts.Schema(type);
            RebaseReferences(schema, location);
            return schema;
        }
        catch (NotSupportedException) { return new JsonObject { ["description"] = "Value in the existing API's JSON format." }; }
    }

    private static void RebaseReferences(JsonNode? node, string location)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is JsonValue reference && reference.TryGetValue<string>(out var text) && text.StartsWith('#'))
                obj["$ref"] = "#/" + location + text[1..];
            foreach (var property in obj.ToArray()) RebaseReferences(property.Value, location);
        }
        else if (node is JsonArray array)
            foreach (var item in array) RebaseReferences(item, location);
    }
}
