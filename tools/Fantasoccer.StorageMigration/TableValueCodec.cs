using System.Globalization;
using System.Text.Json;
using Azure.Data.Tables;

namespace Fantasoccer.StorageMigration;

public static class TableValueCodec
{
    public static TableRow Encode(TableEntity entity) => new(entity.PartitionKey, entity.RowKey,
        entity.Timestamp, entity.ETag.ToString(), entity
            .Where(property => property.Key is not ("PartitionKey" or "RowKey" or "Timestamp" or "odata.etag"))
            .ToDictionary(property => property.Key, property => EncodeValue(property.Value)));

    public static TableEntity Decode(TableRow row)
    {
        var entity = new TableEntity(row.PartitionKey, row.RowKey);
        foreach (var (key, value) in row.Properties)
        {
            if (key is "PartitionKey" or "RowKey" or "Timestamp" or "odata.etag")
                throw new InvalidDataException("A table property cannot replace a system field.");
            entity[key] = DecodeValue(value);
        }
        // Source ETags and timestamps are provenance only; Azure assigns new values on restore.
        return entity;
    }

    private static TableValue EncodeValue(object? value) => value switch
    {
        null => Value("Null", (string?)null),
        string text => Value("Edm.String", text),
        bool boolean => Value("Edm.Boolean", boolean),
        int number => Value("Edm.Int32", number),
        long number => Value("Edm.Int64", number.ToString(CultureInfo.InvariantCulture)),
        double number => Value("Edm.Double", number.ToString("R", CultureInfo.InvariantCulture)),
        Guid guid => Value("Edm.Guid", guid.ToString("D")),
        DateTimeOffset date => Value("Edm.DateTime", date.ToUniversalTime().ToString("O")),
        DateTime date when date.Kind == DateTimeKind.Utc => Value("Edm.DateTime", new DateTimeOffset(date).ToString("O")),
        byte[] bytes => Value("Edm.Binary", Convert.ToBase64String(bytes)),
        BinaryData bytes => Value("Edm.Binary", Convert.ToBase64String(bytes.ToArray())),
        _ => throw new InvalidDataException("Unsupported Table property type; refusing lossy conversion.")
    };

    private static TableValue Value<T>(string type, T value) => new(type, JsonSerializer.SerializeToElement(value));

    private static object? DecodeValue(TableValue value) => value.Type switch
    {
        "Null" when value.Value.ValueKind == JsonValueKind.Null => null,
        "Edm.String" => value.Value.GetString(),
        "Edm.Boolean" => value.Value.GetBoolean(),
        "Edm.Int32" => value.Value.GetInt32(),
        "Edm.Int64" => long.Parse(value.Value.GetString()!, CultureInfo.InvariantCulture),
        "Edm.Double" => double.Parse(value.Value.GetString()!, CultureInfo.InvariantCulture),
        "Edm.Guid" => Guid.Parse(value.Value.GetString()!),
        "Edm.DateTime" => DateTimeOffset.ParseExact(value.Value.GetString()!, "O", CultureInfo.InvariantCulture),
        "Edm.Binary" => Convert.FromBase64String(value.Value.GetString()!),
        _ => throw new InvalidDataException("Unknown Table property type in dump.")
    };
}
