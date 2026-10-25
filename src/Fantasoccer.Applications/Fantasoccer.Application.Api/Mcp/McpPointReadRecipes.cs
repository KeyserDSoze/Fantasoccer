using System.Text.Json;

namespace Fantasoccer.Application.Api.Mcp;

public sealed record McpPointReadRecipe(string Path, string KeyModel,
    IReadOnlyDictionary<string, string> KeySources, JsonElement ExampleArguments, string Notes);

// Public navigation documentation only: examples contain placeholders, never
// real account data. These recipes describe authorized existing Get endpoints.
public static class McpPointReadRecipes
{
    private const string GroupSource = "get_my_account.groups[].i is a group ID. Fetch that group with api/Group/Get; do not search all groups.";
    private const string LeagueSource = "Group.l[].i is the league ID. Select the requested league by name/ID and verify target year in that league's .y[].y.";
    private const string YearSource = "Season index: use Calendar.y for the referenced match, or the requested/current season confirmed in the group's annual data. Never use 2026 in place of season 15.";
    private const string BasketSource = "Use the chosen League.b basket IDs. Within Group.b[] with matching .i, select .y[] where .y equals the target season and find .t[] for the target owner; use that basket's .i.";
    private const string OwnerSource = "Use AnnualTeam.o (actual owner email), or CalendarGame.o/u (home/away owner emails) confirmed against annual teams. Team names and Group.u[].u usernames are not key emails; Group.u[].e maps usernames to emails.";
    private const string DaySource = "Use the containing Calendar Day.a (SerieADay, 1..38), not Day.n or CalendarGame.n (fantasy round/game number) and not a global dayId.";

    public static IReadOnlyList<McpPointReadRecipe> All { get; } =
    [
        new("api/Group/Get", typeof(string).FullName!, new Dictionary<string, string> { ["key"] = GroupSource },
            Arguments("GROUP_ID"),
            "Read the group document as the lookup index for leagues, baskets, annual teams, users and owner emails. The group does not contain the players of each roster: follow its IDs to Team/Get."),
        new("api/Calendar/Get", McpJsonContracts.ModelId(typeof(LeagueKey)), LeagueSources(),
            Arguments("""{"g":"GROUP_ID","l":"LEAGUE_ID","y":15}"""),
            "Read the league calendar by group+league+season. Calendar.y is the season, Day.a the Serie A matchday, and CalendarGame.o/u the actual owner emails. Resolve an opponent from these fields, not from its display name."),
        new("api/Team/Get", McpJsonContracts.ModelId(typeof(TeamKey)), TeamSources(),
            Arguments("""{"g":"GROUP_ID","y":15,"b":"BASKET_ID","e":"OWNER_EMAIL"}"""),
            "Read the current seasonal roster by group+season+basket+owner email. Resolve basket and owner in the group document; no league field belongs in TeamKey. Use this point read, never Team/Query, to retrieve players."),
        new("api/Team/day/Get", McpJsonContracts.ModelId(typeof(TeamDayKey)), WithDay(TeamSources()),
            Arguments("""{"g":"GROUP_ID","y":15,"b":"BASKET_ID","d":9,"e":"OWNER_EMAIL"}"""),
            "Read the saved lineup/snapshot for a specific Serie A matchday. If it does not exist, read api/Team/Get for the seasonal roster and clearly disclose the fallback; do not claim it is the saved matchday formation."),
        new("api/Rank/Get", McpJsonContracts.ModelId(typeof(LeagueKey)), LeagueSources(),
            Arguments("""{"g":"GROUP_ID","l":"LEAGUE_ID","y":15}"""),
            "Read standings directly by group+league+season. The group's leagues provide league IDs; arbitrary repository searches or aggregations are unnecessary."),
        new("api/Rank/day/Get", McpJsonContracts.ModelId(typeof(YearDayLeague)), WithDay(LeagueSources()),
            Arguments("""{"y":15,"d":9,"g":"GROUP_ID","l":"LEAGUE_ID"}"""),
            "Read the rank snapshot for the referenced Serie A matchday using the daily factory. Do not replace the matchday with the fantasy round number."),
        new("api/VotedRealPlayerWrapper/official/Get", McpJsonContracts.ModelId(typeof(YearDayKey)),
            new Dictionary<string, string> { ["y"] = YearSource, ["d"] = DaySource },
            Arguments("""{"y":15,"d":9}"""),
            "Read official votes by season+Serie A matchday. The live factory is provisional; use the exact factory discovered in the catalog."),
        new("api/RealPlayersWrapper/Get", typeof(int).FullName!, new Dictionary<string, string> { ["key"] = YearSource },
            Arguments("15"),
            "Read the season's real-player wrapper by simple season key, then select players in that returned document. This wrapper is not a fantasy team's owned roster.")
    ];

    public static McpPointReadRecipe? Find(string path)
        => All.FirstOrDefault(x => x.Path.Equals(path.Split('?')[0], StringComparison.OrdinalIgnoreCase));

    private static JsonElement Arguments(string key) => JsonSerializer.SerializeToElement(new { query = new { key } });
    private static Dictionary<string, string> LeagueSources() => new() { ["g"] = GroupSource, ["l"] = LeagueSource, ["y"] = YearSource };
    private static Dictionary<string, string> TeamSources() => new() { ["g"] = GroupSource, ["y"] = YearSource, ["b"] = BasketSource, ["e"] = OwnerSource };
    private static Dictionary<string, string> WithDay(Dictionary<string, string> sources)
    {
        sources["d"] = DaySource;
        return sources;
    }
}
