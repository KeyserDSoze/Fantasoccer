using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class McpDomainContextTests(ApiFactory factory)
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, McpJsonContracts.Json);

    [Theory]
    [InlineData("2012-08-10T00:00:00Z", 1, "2012/13", 1, 38)]
    [InlineData("2026-01-01T00:00:00Z", 14, "2025/26", 495, 532)]
    [InlineData("2026-08-09T23:59:59Z", 14, "2025/26", 495, 532)]
    [InlineData("2026-08-10T00:00:00Z", 15, "2026/27", 533, 570)]
    [InlineData("2026-10-24T12:00:00Z", 15, "2026/27", 533, 570)]
    [InlineData("2027-01-01T00:00:00Z", 15, "2026/27", 533, 570)]
    [InlineData("2027-08-10T00:00:00Z", 16, "2027/28", 571, 608)]
    [InlineData("2026-08-10T01:00:00+02:00", 14, "2025/26", 495, 532)]
    public void Current_season_uses_the_shared_UTC_rule_not_calendar_year_or_client_timezone(
        string instant, int index, string label, int firstDay, int lastDay)
    {
        var now = DateTimeOffset.Parse(instant);
        var context = new FantasoccerSeasonContext(new FrozenTime(now));
        var snapshot = context.GetCurrent();
        Assert.Equal(now, snapshot.ServerTimeUtc);
        Assert.Equal(index, snapshot.CurrentSeason.SeasonIndex);
        Assert.Equal(FantacalcioTime.SeasonYearAt(now.UtcDateTime), index);
        Assert.Equal(label, snapshot.CurrentSeason.Label);
        Assert.Equal(FantacalcioTime.LabelYear(index), snapshot.CurrentSeason.FullLabel);
        Assert.Equal(firstDay, snapshot.CurrentSeason.FirstGlobalDayId);
        Assert.Equal(lastDay, snapshot.CurrentSeason.LastGlobalDayId);
        Assert.Equal(new DateTimeOffset(2011 + index, 8, 10, 0, 0, 0, TimeSpan.Zero), snapshot.CurrentSeason.StartsAtUtc);
        Assert.Equal(snapshot.CurrentSeason.StartsAtUtc.AddYears(1), snapshot.CurrentSeason.EndsAtUtcExclusive);
    }

    [Theory]
    [InlineData("Team", "p", "Players")]
    [InlineData("Player", "p", "Price")]
    [InlineData("Player", "r", "Role")]
    [InlineData("TeamKey", "g", "Group")]
    [InlineData("AppIdentity", "g", "Groups")]
    [InlineData("VoteLeagueSetting", "g", "Goal")]
    [InlineData("TeamResponse", "i", "IsOk")]
    public void Glossary_maps_the_real_model_specific_JSON_aliases(string model, string alias, string name)
    {
        var result = Json(factory.Services.GetRequiredService<McpModelCatalog>().Describe(model));
        var field = Assert.Single(result.GetProperty("fields").EnumerateArray(), x => x.GetProperty("jsonName").GetString() == alias);
        Assert.Equal(name, field.GetProperty("name").GetString());
        Assert.Contains(name, field.GetProperty("description").GetString());
    }

    [Fact]
    public void Model_metadata_preserves_ignored_properties_nested_types_and_enum_wire_values()
    {
        var catalog = factory.Services.GetRequiredService<McpModelCatalog>();
        var player = Json(catalog.Describe("Player"));
        Assert.DoesNotContain(player.GetProperty("fields").EnumerateArray(), x => x.GetProperty("name").GetString() == "Key");
        var role = Assert.Single(player.GetProperty("fields").EnumerateArray(), x => x.GetProperty("name").GetString() == "Role");
        Assert.Contains("Fantasoccer.Role", role.GetProperty("models").EnumerateArray().Select(x => x.GetString()));
        var identityRole = Json(catalog.Describe("IdentityRole"));
        Assert.True(identityRole.GetProperty("flags").GetBoolean());
        var admin = Assert.Single(identityRole.GetProperty("enumValues").EnumerateArray(), x => x.GetProperty("name").GetString() == "Admin");
        Assert.Equal(4, admin.GetProperty("wireValue").GetInt32());
        var command = Json(catalog.Describe("AuctionCommand"));
        var bid = Assert.Single(command.GetProperty("enumValues").EnumerateArray(), x => x.GetProperty("name").GetString() == "PlaceBid");
        Assert.Equal("PlaceBid", bid.GetProperty("wireValue").GetString());
        Assert.Contains("Name", player.GetProperty("schema").GetProperty("properties").GetProperty("n").GetProperty("description").GetString());
    }

    [Fact]
    public void Metadata_is_paginated_and_does_not_expose_internal_OAuth_models()
    {
        var catalog = factory.Services.GetRequiredService<McpModelCatalog>();
        var first = Json(catalog.List(limit: 1000));
        Assert.True(first.GetProperty("total").GetInt32() > 50);
        Assert.Equal(50, first.GetProperty("items").GetArrayLength());
        Assert.Empty(Json(catalog.List(offset: int.MaxValue)).GetProperty("items").EnumerateArray());
        Assert.Empty(Json(catalog.List("OAuth")).GetProperty("items").EnumerateArray());
        Assert.Empty(Json(catalog.List("McpOptions")).GetProperty("items").EnumerateArray());
        Assert.True(Json(catalog.Describe("OAuthDocument")).TryGetProperty("error", out _));
        Assert.True(Json(catalog.Describe("invented")).TryGetProperty("error", out _));
    }

    [Fact]
    public void Every_discovered_model_has_a_serializable_schema_and_all_operation_responses_can_be_documented()
    {
        var models = factory.Services.GetRequiredService<McpModelCatalog>();
        var first = Json(models.List(limit: 50));
        for (var offset = 0; offset < first.GetProperty("total").GetInt32(); offset += 50)
        {
            foreach (var model in Json(models.List(offset: offset, limit: 50)).GetProperty("items").EnumerateArray())
            {
                var description = Json(models.Describe(model.GetProperty("model").GetString()!));
                Assert.True(description.TryGetProperty("schema", out _), model.GetRawText());
                Assert.False(description.TryGetProperty("error", out _));
            }
        }
        var catalog = factory.Services.GetRequiredService<McpEndpointCatalog>();
        var tools = new McpCatalogTools(catalog, factory.Services.GetRequiredService<McpApiFacade>());
        for (var offset = 0; offset < catalog.McpOperations.Count; offset += 20)
        {
            var page = Json(tools.ListOperations(offset: offset, limit: 20));
            foreach (var operation in page.GetProperty("items").EnumerateArray())
            {
                Assert.True(operation.TryGetProperty("responseSchemas", out _));
                Assert.True(operation.TryGetProperty("inputModels", out _));
            }
        }
    }

    [Theory]
    [InlineData("api/Team/Get", "TeamKey")]
    [InlineData("api/Team/day/Get", "TeamDayKey")]
    [InlineData("api/Team/opening/Get", "OpeningTeamKey")]
    [InlineData("api/Team/openingdaily/Get", "OpeningTeamDayKey")]
    public void Operation_catalog_describes_the_real_key_type_of_each_repository_factory(string path, string keyType)
    {
        var operation = factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values.Single(x => x.Api.RelativePath == path);
        Assert.NotNull(operation.Repository);
        Assert.Equal(keyType, operation.Repository.KeyType.Name);
        var key = operation.Tool.InputSchema.GetProperty("properties").GetProperty("query").GetProperty("properties").GetProperty("key");
        Assert.Contains(keyType, key.GetProperty("description").GetString());
    }

    [Fact]
    public void Annotated_input_schemas_keep_compact_fields_and_season_meanings()
    {
        var operations = factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values;
        var save = operations.Single(x => x.Api.RelativePath == "Game/SaveTeam");
        var properties = save.Tool.InputSchema.GetProperty("properties");
        Assert.Contains("15=2026/27", properties.GetProperty("query").GetProperty("properties").GetProperty("year").GetProperty("description").GetString());
        var player = properties.GetProperty("body").GetProperty("properties").GetProperty("p").GetProperty("items");
        Assert.Contains("Price", player.GetProperty("properties").GetProperty("p").GetProperty("description").GetString());
        Assert.Contains("PlayerInTeamStatus", player.GetProperty("properties").GetProperty("s").GetProperty("description").GetString());
    }

    private sealed class FrozenTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
