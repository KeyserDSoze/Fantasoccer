using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpContextTools(FantasoccerSeasonContext seasons, McpModelCatalog models)
{
    [McpServerTool(Name = "get_current_season", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get the current Fantasoccer season from server UTC time: seasonIndex, label (e.g. 2026/27=15), season boundaries and day-ID conventions. year/y in APIs normally means this 1-based index, not 2026. Saved account selections may be historical; check the group's configured seasons. This does not claim that a football match is live.")]
    public FantasoccerSeasonSnapshot GetCurrentSeason() => seasons.GetCurrent();

    [McpServerTool(Name = "get_fantasoccer_guide", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read how Fantasoccer works. Start with lookups to follow NoSQL/Blob point reads: account -> group -> league/basket/year/owner -> Team/Get or Team/day/Get; calendar owner emails and Serie A matchday resolve opponents without generic queries. Other topics: overview, seasons, payloads, groups, formations, market, auctions, scores, opening, administration, personal. topic=models lists JSON models. Metadata search is not a document scan. Documentation grants no permission to write.")]
    public object GetGuide(
        [Description("Guide topic; use models to list model IDs, with optional filter/offset/limit.")] string topic = "overview",
        string? filter = null, int offset = 0, int limit = 20)
    {
        var index = FantasoccerGuide.Sections.Select(x => new { x.Topic, x.Title })
            .Append(new { Topic = "models", Title = "Dizionario dei modelli JSON esposti" });
        return new
        {
            topics = index,
            section = FantasoccerGuide.Sections.FirstOrDefault(x => x.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)),
            pointReadRecipes = topic.Equals("lookups", StringComparison.OrdinalIgnoreCase) ? McpPointReadRecipes.All : null,
            models = topic.Equals("models", StringComparison.OrdinalIgnoreCase) ? models.List(filter, offset, limit) : null,
            error = topic.Equals("models", StringComparison.OrdinalIgnoreCase) || FantasoccerGuide.Sections.Any(x => x.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase))
                ? null : "Unknown topic; choose one from topics."
        };
    }

    [McpServerTool(Name = "describe_api_model", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Decode a Fantasoccer JSON payload model: maps compact JSON names to full property names/meanings/types, nested model IDs, read-only flags, schema and actual serialized enum values. Use the model/keyType/inputModels/responseSchemas IDs from list_api_operations, or get_fantasoccer_guide(topic=models). Aliases like g/r/p mean different things per model; never infer them globally. Returns metadata only, no user data.")]
    public object DescribeModel([Description("Full model ID or unique type name, e.g. Fantasoccer.Team, TeamKey, Market or IdentityRole.")] string model)
        => models.Describe(model);
}
