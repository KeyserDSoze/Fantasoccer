using System.Text.Json;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

// Literal archived shapes, not fixtures produced by the converter under test.
public sealed class LegacyEnvelopeTests
{
    [Theory]
    [InlineData("{\"k\":{\"y\":15,\"d\":8},\"v\":\"archived value\"}")]
    [InlineData("{\"Key\":{\"y\":15,\"d\":8},\"Value\":\"archived value\"}")]
    public void Reads_both_historical_envelope_formats_without_changing_key_aliases(string json)
    {
        var entity = JsonSerializer.Deserialize<LegacyEntity<string, YearDayKey>>(json)!;
        Assert.Equal(15, entity.Key!.Year);
        Assert.Equal(8, entity.Key.Day);
        Assert.Equal("archived value", entity.Value);
        Assert.Equal("{\"k\":{\"y\":15,\"d\":8},\"v\":\"archived value\"}", JsonSerializer.Serialize(entity));
    }

    [Fact]
    public void Short_names_take_precedence_and_unknown_fields_do_not_change_the_document()
    {
        var entity = JsonSerializer.Deserialize<LegacyEntity<string, int>>("{\"k\":15,\"Key\":14,\"v\":\"current\",\"Value\":\"old\",\"other\":true}")!;
        Assert.Equal(15, entity.Key);
        Assert.Equal("current", entity.Value);
    }
}
