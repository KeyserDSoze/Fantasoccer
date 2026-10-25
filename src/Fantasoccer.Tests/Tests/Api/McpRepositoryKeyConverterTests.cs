using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class McpRepositoryKeyConverterTests
{
    public static TheoryData<Type> DomainKeyTypes()
    {
        var result = new TheoryData<Type>();
        foreach (var type in typeof(TeamKey).Assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && typeof(IDefaultKey).IsAssignableFrom(x)))
            result.Add(type);
        return result;
    }

    [Theory]
    [MemberData(nameof(DomainKeyTypes))]
    public void Every_domain_composite_key_uses_its_real_Rystem_wire_contract(Type type)
    {
        var key = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties().Where(x => x.SetMethod != null))
        {
            if (property.PropertyType == typeof(string)) property.SetValue(key, property.Name + "-value");
            else if (property.PropertyType == typeof(int)) property.SetValue(key, property.Name == "Day" ? 3 : 15);
            else Assert.Fail("Add an explicit sample for key field " + property.Name);
        }
        var json = JsonSerializer.SerializeToElement(key, type, McpJsonContracts.Json);
        var converted = McpRepositoryKeyConverter.Convert(type, json.GetRawText());
        Assert.True(JsonElement.DeepEquals(json, JsonSerializer.Deserialize<JsonElement>(converted.CanonicalJson)));
        Assert.DoesNotContain('{', converted.WireValue);
        Assert.Equal(json.EnumerateObject().Count(), converted.WireValue.Split("|||").Length);
        var schema = McpJsonContracts.Schema(type).AsObject();
        Assert.Equal(json.EnumerateObject().Count(), schema["required"]!.AsArray().Count);
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("{\"Group\":\"group\",\"League\":\"league\",\"Year\":15}")]
    [InlineData("{\"Y\":\"15\",\"L\":\"league\",\"G\":\"group\"}")]
    [InlineData("{\"year\":15,\"league\":\"league\",\"group\":\"group\"}")]
    public void Aliases_casing_and_input_order_canonicalize_before_authorization(string input)
    {
        var converted = McpRepositoryKeyConverter.Convert(typeof(LeagueKey), input);
        var expected = new LeagueKey { Group = "group", League = "league", Year = 15 };
        Assert.Equal(JsonSerializer.Serialize(expected), converted.CanonicalJson);
        Assert.Equal(KeySettings<LeagueKey>.Instance.AsString(expected), converted.WireValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\"}")]
    [InlineData("{\"g\":null,\"l\":\"league\",\"y\":15}")]
    [InlineData("{\"g\":\" \",\"l\":\"league\",\"y\":15}")]
    [InlineData("{\"g\":{},\"l\":\"league\",\"y\":15}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":\"not-a-number\"}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":2147483648}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":15,\"y\":15}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":15,\"G\":\"other\"}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":15,\"Group\":\"group\"}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league\",\"y\":15,\"z\":0}")]
    [InlineData("{\"g\":\"group|||other\",\"l\":\"league\",\"y\":15}")]
    [InlineData("{\"g\":\"group\",\"l\":\"league|||other\",\"y\":15}")]
    public void Malformed_missing_ambiguous_and_separator_injected_keys_fail_closed(string input)
    {
        Assert.Throws<McpRepositoryKeyException>(() => McpRepositoryKeyConverter.Convert(typeof(LeagueKey), input));
    }

    [Fact]
    public void Excessive_key_length_and_non_key_types_are_rejected()
    {
        Assert.Throws<McpRepositoryKeyException>(() => McpRepositoryKeyConverter.Convert(typeof(LeagueKey), new string('x', 16385)));
        Assert.Throws<McpRepositoryKeyException>(() => McpRepositoryKeyConverter.Convert(typeof(Team), "{}"));
    }
}
