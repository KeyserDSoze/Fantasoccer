using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

public sealed record McpConvertedRepositoryKey(string CanonicalJson, string WireValue);

public sealed class McpRepositoryKeyException() : ArgumentException(
    "Invalid repository key. Supply a JSON-encoded object matching repository.keySchema, with all key fields and no unknown or repeated aliases. Do not construct the native repository key yourself.");

// MCP documents composite keys as JSON for the model. Rystem's HTTP binder
// instead expects KeySettings<TKey>.AsString (normally positional '|||' values).
// Authorize the canonical JSON and dispatch its exact typed wire equivalent.
public static class McpRepositoryKeyConverter
{
    private static readonly ConcurrentDictionary<Type, Func<string, McpConvertedRepositoryKey>> Converters = new();

    public static McpConvertedRepositoryKey Convert(Type keyType, string json)
    {
        if (json.Length > 16 * 1024 || !typeof(IDefaultKey).IsAssignableFrom(keyType)) throw new McpRepositoryKeyException();
        return Converters.GetOrAdd(keyType, static type => typeof(McpRepositoryKeyConverter)
            .GetMethod(nameof(ConvertTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type).CreateDelegate<Func<string, McpConvertedRepositoryKey>>())(json);
    }

    private static McpConvertedRepositoryKey ConvertTyped<TKey>(string json) where TKey : class, IDefaultKey
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new McpRepositoryKeyException();
            var fields = McpJsonContracts.Json.GetTypeInfo(typeof(TKey)).Properties.Where(x => x.Set != null).ToArray();
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in document.RootElement.EnumerateObject())
            {
                var property = fields.SingleOrDefault(x =>
                    x.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase) ||
                    (x.AttributeProvider as MemberInfo)?.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase) == true);
                if (property == null || !values.TryAdd(property.Name, field.Value) ||
                    field.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                    (property.PropertyType == typeof(string) &&
                        (field.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(field.Value.GetString()))))
                    throw new McpRepositoryKeyException();
            }
            // Even keys whose CLR properties predate 'required' need every component.
            // Do not silently turn a missing season into 0 or a missing owner into null.
            if (values.Count != fields.Length || fields.Length == 0) throw new McpRepositoryKeyException();
            var key = JsonSerializer.SerializeToElement(values).Deserialize<TKey>(McpJsonContracts.Json)
                ?? throw new McpRepositoryKeyException();
            var canonical = JsonSerializer.SerializeToElement(key, McpJsonContracts.Json);
            var wire = KeySettings<TKey>.Instance.AsString(key);
            var roundtrip = JsonSerializer.SerializeToElement(KeySettings<TKey>.Instance.Parse(wire), McpJsonContracts.Json);
            // Reject separator injection or any lossy/custom conversion: authorization
            // and the repository must refer to exactly the same key components.
            if (!JsonElement.DeepEquals(canonical, roundtrip)) throw new McpRepositoryKeyException();
            return new(canonical.GetRawText(), wire);
        }
        catch (McpRepositoryKeyException) { throw; }
        catch (Exception exception) when (exception.GetBaseException() is JsonException or ArgumentException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            // Never expose parser internals or echo keys/owner emails in errors.
            throw new McpRepositoryKeyException();
        }
    }
}
