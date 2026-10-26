using Azure.Data.Tables;
using Fantasoccer.StorageMigration;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class TableValueCodecTests
{
    [Fact]
    public void All_Table_types_round_trip_without_precision_loss()
    {
        var original = new TableEntity("partition", "row")
        {
            ["Text"] = "àè 日本語",
            ["Boolean"] = true,
            ["Int32"] = int.MaxValue,
            ["Int64"] = long.MaxValue,
            ["Double"] = double.Epsilon,
            ["NaN"] = double.NaN,
            ["Infinity"] = double.PositiveInfinity,
            ["Guid"] = Guid.NewGuid(),
            ["Date"] = DateTimeOffset.Parse("2026-10-25T12:34:56.1234567+02:00"),
            ["Binary"] = new byte[] { 0, 127, 255 },
            ["Null"] = null
        };
        var restored = TableValueCodec.Decode(TableValueCodec.Encode(original));
        Assert.Equal(original.PartitionKey, restored.PartitionKey);
        Assert.Equal(original.RowKey, restored.RowKey);
        foreach (var (key, value) in original)
        {
            Assert.Equal(value?.GetType(), restored[key]?.GetType());
            if (value is byte[] bytes) Assert.Equal(bytes, Assert.IsType<byte[]>(restored[key]));
            else Assert.Equal(value, restored[key]);
        }
    }

    [Fact]
    public void Unsupported_types_are_not_silently_converted_to_strings()
    {
        var original = new TableEntity("partition", "row") { ["Decimal"] = 1.5m };
        Assert.Throws<InvalidDataException>(() => TableValueCodec.Encode(original));
    }

    [Fact]
    public void Credentials_are_not_included_in_source_identity_and_remote_restore_is_refused()
    {
        var source = new StorageEndpoints("BlobEndpoint=https://source.blob.core.windows.net;" +
                                          "TableEndpoint=https://source.table.core.windows.net;" +
                                          "SharedAccessSignature=sv=2025-01-05&sig=private-test-signature");
        Assert.DoesNotContain("sig=", source.SourceId);
        Assert.DoesNotContain("private-test-signature", source.SourceId);
        Assert.Throws<InvalidOperationException>(() => source.RequireLocalEmulator());
        Assert.Throws<InvalidOperationException>(() => source.RequireExportPermission(allowRemoteSource: false));
        source.RequireExportPermission(allowRemoteSource: true);
    }
}
