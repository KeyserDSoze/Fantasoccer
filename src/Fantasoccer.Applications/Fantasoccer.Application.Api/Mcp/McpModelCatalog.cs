using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

// Build only from exposed operations and their reachable wire types. Never scan
// all services/assemblies (which also contain OAuth storage and signing options).
public sealed class McpModelCatalog(McpEndpointCatalog catalog)
{
    private readonly Lazy<IReadOnlyDictionary<string, Type>> _models = new(() => Build(catalog));

    public object List(string? filter = null, int offset = 0, int limit = 20)
    {
        var types = _models.Value.Where(x => string.IsNullOrEmpty(filter) || x.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        return new
        {
            total = types.Length, offset = Math.Max(0, offset),
            items = types.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 50))
                .Select(x => new { model = x.Key, name = McpJsonContracts.TypeName(x.Value), kind = x.Value.IsEnum ? "enum" : "object" })
        };
    }

    public object Describe(string model)
    {
        var types = _models.Value.Where(x => x.Key == model || x.Value.Name == model || McpJsonContracts.TypeName(x.Value) == model).ToArray();
        if (types.Length != 1) return new
        {
            error = types.Length == 0 ? "Unknown model. Use get_fantasoccer_guide(topic='models', filter=...) to find exposed types." : "Ambiguous model. Use its full model ID.",
            candidates = types.Select(x => x.Key).ToArray()
        };
        var type = types[0].Value;
        var info = McpJsonContracts.Json.GetTypeInfo(type);
        return new
        {
            model = McpJsonContracts.ModelId(type), name = McpJsonContracts.TypeName(type),
            note = "JSON aliases are model-specific. Fields marked readOnly/computed must not be treated as writable. Consult the operation input schema and workflow before writing.",
            fields = info.Kind == JsonTypeInfoKind.Object ? info.Properties.Where(x => x.Get != null || x.Set != null).Select(property => new
            {
                jsonName = property.Name,
                name = (property.AttributeProvider as System.Reflection.MemberInfo)?.Name ?? property.Name,
                type = McpJsonContracts.TypeName(property.PropertyType),
                description = McpJsonContracts.PropertyDescription(property),
                readOnly = property.Set == null,
                required = property.IsRequired || (property.Set != null && typeof(IDefaultKey).IsAssignableFrom(type)),
                models = ReferencedTypes(property.PropertyType).Where(t => _models.Value.ContainsKey(McpJsonContracts.ModelId(t)))
                    .Select(McpJsonContracts.ModelId).Distinct().ToArray()
            }).ToArray() : null,
            flags = type.IsDefined(typeof(FlagsAttribute), inherit: false),
            enumValues = type.IsEnum ? Enum.GetNames(type).Select(name => new
            {
                name, wireValue = JsonSerializer.SerializeToElement(Enum.Parse(type, name), type, McpJsonContracts.Json)
            }).ToArray() : null,
            schema = McpJsonContracts.Schema(type)
        };
    }

    public static IEnumerable<Type> ReferencedTypes(Type type)
    {
        if (type.IsArray) return ReferencedTypes(type.GetElementType()!);
        if (type.IsGenericType) return type.GetGenericArguments().SelectMany(ReferencedTypes)
            .Concat(IsModel(type) ? [type] : Array.Empty<Type>());
        return IsModel(type) ? [type] : [];
    }

    private static bool IsModel(Type type) => type != typeof(object) && type != typeof(JsonElement) &&
        !type.IsPrimitive && !type.IsInterface &&
        (type.Assembly == typeof(Group).Assembly ||
         type.Namespace == "Fantasoccer.Application.Api.Controllers" || type.Namespace == typeof(FantasoccerSeason).Namespace ||
         type.Namespace?.StartsWith("RepositoryFramework", StringComparison.Ordinal) == true) &&
        type.Namespace?.Contains(".Storage", StringComparison.Ordinal) != true;

    private static IReadOnlyDictionary<string, Type> Build(McpEndpointCatalog catalog)
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        void Visit(Type type)
        {
            foreach (var model in ReferencedTypes(type))
            {
                if (!types.TryAdd(McpJsonContracts.ModelId(model), model)) continue;
                var info = McpJsonContracts.Json.GetTypeInfo(model);
                if (info.Kind == JsonTypeInfoKind.Object)
                    foreach (var property in info.Properties.Where(x => x.Get != null || x.Set != null)) Visit(property.PropertyType);
            }
        }
        foreach (var operation in catalog.Operations.Values)
        {
            foreach (var parameter in operation.Api.ParameterDescriptions.Where(x => x.Source != BindingSource.Services)) Visit(parameter.Type);
            foreach (var response in operation.Api.SupportedResponseTypes.Where(x => x.Type != null)) Visit(response.Type!);
            if (operation.Repository is { } repository)
            {
                Visit(repository.ModelType);
                Visit(repository.KeyType);
            }
        }
        return types;
    }
}
