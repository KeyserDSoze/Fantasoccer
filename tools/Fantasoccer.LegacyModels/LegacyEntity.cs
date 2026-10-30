using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fantasoccer.LegacyModels;

// The envelope the legacy repositories stored around every document: {"k": key, "v": value}. Documents written by
// even older versions used the long names {"Key": ..., "Value": ...}; both are read, only the short form is written.
[JsonConverter(typeof(LegacyEntityJsonConverterFactory))]
public sealed class LegacyEntity<T, TKey> where TKey : notnull
{
    public LegacyEntity(T? value, TKey? key) { Value = value; Key = key; }
    public TKey? Key { get; set; }
    public T? Value { get; set; }
    public static LegacyEntity<T, TKey> Default(T value, TKey key) => new(value, key);
}

public sealed class LegacyEntityJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(LegacyEntity<,>);

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(LegacyEntityJsonConverter<,>).MakeGenericType(type.GetGenericArguments()))!;
}

public sealed class LegacyEntityJsonConverter<T, TKey> : JsonConverter<LegacyEntity<T, TKey>> where TKey : notnull
{
    public override LegacyEntity<T, TKey>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        TKey? key = default;
        T? value = default;
        if (root.TryGetProperty("k", out var shortKey)) key = JsonSerializer.Deserialize<TKey>(shortKey.GetRawText(), options);
        else if (root.TryGetProperty("Key", out var longKey)) key = JsonSerializer.Deserialize<TKey>(longKey.GetRawText(), options);
        if (root.TryGetProperty("v", out var shortValue)) value = JsonSerializer.Deserialize<T>(shortValue.GetRawText(), options);
        else if (root.TryGetProperty("Value", out var longValue)) value = JsonSerializer.Deserialize<T>(longValue.GetRawText(), options);
        return new(value, key);
    }

    public override void Write(Utf8JsonWriter writer, LegacyEntity<T, TKey> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("k");
        JsonSerializer.Serialize(writer, value.Key, options);
        writer.WritePropertyName("v");
        JsonSerializer.Serialize(writer, value.Value, options);
        writer.WriteEndObject();
    }
}
