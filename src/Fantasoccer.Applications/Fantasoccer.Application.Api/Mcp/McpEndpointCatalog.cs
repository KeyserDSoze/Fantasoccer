using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ModelContextProtocol.Protocol;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

public sealed record McpRepositoryContract(Type ModelType, Type KeyType, string Factory);
public sealed record McpOperation(ApiDescription Api, Tool Tool, bool ReadOnly, McpRepositoryContract? Repository);

public sealed class McpEndpointCatalog(IApiDescriptionGroupCollectionProvider descriptions, RepositoryFrameworkRegistry registry)
{
    private readonly Lazy<IReadOnlyDictionary<string, McpOperation>> _operations = new(() => Build(descriptions, registry));
    public IReadOnlyDictionary<string, McpOperation> Operations => _operations.Value;
    private static IReadOnlyDictionary<string, McpOperation> Build(IApiDescriptionGroupCollectionProvider descriptions, RepositoryFrameworkRegistry registry)
    {
        var result = new Dictionary<string, McpOperation>(StringComparer.Ordinal);
        foreach (var api in descriptions.ApiDescriptionGroups.Items.SelectMany(x => x.Items))
        {
            var path = api.RelativePath?.Split('?')[0];
            if (path == null || api.HttpMethod == null || path.StartsWith("api/Authentication/", StringComparison.OrdinalIgnoreCase)) continue;
            // Only existing application controllers and generated repositories, never auth, Swagger, MCP or arbitrary URLs.
            var controller = api.ActionDescriptor as ControllerActionDescriptor;
            if (!(path.StartsWith("api/", StringComparison.OrdinalIgnoreCase) ||
                  controller?.ControllerTypeInfo.Namespace == "Fantasoccer.Application.Api.Controllers")) continue;

            var parts = path.Split('/');
            var factory = parts.Length >= 4 ? parts[2] : "";
            var repository = controller != null || parts.Length < 3 ? null : registry.Services.Values
                .Where(x => x.ModelType.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.FactoryName ?? "", factory, StringComparison.OrdinalIgnoreCase))
                .Select(x => new McpRepositoryContract(x.ModelType, x.KeyType, x.FactoryName ?? "")).Distinct().SingleOrDefault();

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
                        if (parameter.Name == "key" && repository != null)
                        {
                            // Rystem exposes the encoded string, not the composite key shape.
                            obj["description"] = repository.KeyType == typeof(string) || repository.KeyType.IsPrimitive
                                ? "Chiave semplice del repository: " + McpJsonContracts.TypeName(repository.KeyType) + "."
                                : "Stringa contenente un oggetto JSON con tutti i campi di " + McpJsonContracts.ModelId(repository.KeyType) +
                                  ". Il server valida la chiave e la converte nel formato nativo Rystem; non costruire manualmente la stringa con separatori. Consultare repository.keySchema/describe_api_model; usare i nomi compatti, senza cambiare factory.";
                        }
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
            var readOnly = IsReadOnly(api.HttpMethod, path);
            var tool = new Tool
            {
                Name = name,
                Description = $"{api.HttpMethod} /{path}. Executes the existing Fantasoccer API with your authenticated account and all API/business authorization checks. " +
                    (readOnly ? "Reads data." : "MAY CHANGE DATA. Obtain the user's explicit instruction before calling; do not retry automatically after an uncertain outcome.") +
                    " For MCP, composite query.key is a JSON-encoded object; the server converts it to the native repository wire format after validating all key fields. year/y means season index (15=2026/27), not calendar year. Use describe_api_model to decode compact fields; identical letters mean different things in different models. No credentials or user impersonation headers are accepted." +
                    (path.StartsWith("McpAuction/") ? " Discover auction command arguments using McpAuction/Commands before invoking a command." : ""),
                InputSchema = JsonSerializer.SerializeToElement(ObjectSchema(properties, required)),
                Annotations = new ToolAnnotations { ReadOnlyHint = readOnly, DestructiveHint = !readOnly, IdempotentHint = readOnly, OpenWorldHint = false }
            };
            result.TryAdd(name, new(api, tool, readOnly, repository));
        }
        return result;
    }

    // Several existing writes use GET. Never infer read-only solely from the HTTP verb.
    public static bool IsReadOnly(string method, string path)
    {
        var action = path.Split('/').Last().ToLowerInvariant();
        if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            return action is "get" or "exist" or "query" or "count";
        return method == "GET" && (action.StartsWith("get") || action is "ping" or "optimalformation" or "team" or "commands" || path.Contains("GetCardById/", StringComparison.Ordinal));
    }

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
