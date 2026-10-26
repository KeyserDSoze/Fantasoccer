using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpContextTools(FantasoccerSeasonContext seasons, McpModelCatalog models, IConfiguration configuration)
{
    [McpServerTool(Name = "get_current_season", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get the current Fantasoccer season from server UTC time: seasonIndex, label (e.g. 2026/27=15), season boundaries and day-ID conventions. year/y in APIs normally means this 1-based index, not 2026. Saved account selections may be historical; check the group's configured seasons. This does not claim that a football match is live.")]
    public FantasoccerSeasonSnapshot GetCurrentSeason() => seasons.GetCurrent();

    [McpServerTool(Name = "get_fantasoccer_guide", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read the guide for the active backend. PostgreSQL uses readable DTOs and UUIDs through /api/groups, /api/teams, /api/football and /api/competitions; legacy mode uses Blob point reads. Start with lookups. topic=models lists JSON model metadata, not user data. Documentation grants no permission to write.")]
    public object GetGuide(
        [Description("Guide topic; use models to list model IDs, with optional filter/offset/limit.")] string topic = "overview",
        string? filter = null, int offset = 0, int limit = 20)
    {
        if (configuration.GetValue<bool>("PostgreSql:Enabled"))
        {
            return new { backend = "PostgreSQL", topics = new[] { "overview", "lookups", "models" },
                note = "Readable DTOs only. Legacy repository operations are not exposed in this mode.",
                lookups = new[] { "get_my_account", "GET /api/groups", "GET /api/groups/{groupId}",
                    "GET /api/teams/roster?groupId=...&basketId=...&year=...&owner=...&kind=0&day=0",
                    "GET /api/football/{year}/calendar", "GET /api/football/{year}/days/{day}/votes/official",
                    "GET /api/competitions/{groupId}/{leagueId}/{year}/calendar",
                    "GET /api/competitions/{groupId}/{leagueId}/{year}/games/{gameId}",
                    "GET /api/competitions/{groupId}/{leagueId}/{year}/rank?day=0" },
                season = "year is the 1-based season index (2012/13=1); day is a Serie A matchday, not a fantasy round.",
                formations = "Send playerId/position, teamId, expectedVersion and sourceVersion from the game/roster DTO. Do not send prices or reconstruct IDs from player names.",
                history = "Saved results and rankings are authoritative. Live resultIsPreview is never saved by a read. Calendar generation/recalculation and opening preparation are explicit POST commands.",
                authorization = "Membership/ownership and roles are re-read from PostgreSQL by the shared application services; never infer access from a saved selection.",
                models = topic.Equals("models", StringComparison.OrdinalIgnoreCase) ? models.List(filter, offset, limit) : null };
        }
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
