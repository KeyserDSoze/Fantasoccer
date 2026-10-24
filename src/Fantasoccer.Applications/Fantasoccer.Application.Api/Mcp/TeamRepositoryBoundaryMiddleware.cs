using System.Security.Claims;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

// Always enabled, including when MCP is disabled. Rystem's global "authenticated"
// policy does not by itself invoke resource-specific repository requirements.
// Public full-document writes must be administrative; trusted application services
// still use repositories directly for formations, market and auction transactions.
public sealed class TeamRepositoryBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RepositoryFrameworkRegistry registry, IRepository<Group, string> groups)
    {
        var parts = context.Request.Path.Value?.Trim('/').Split('/') ?? [];
        if (parts.Length < 3 || !parts[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals("Team", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }
        var method = (parts[^1].Equals("Stream", StringComparison.OrdinalIgnoreCase) ? parts[^2] : parts[^1]).ToUpperInvariant();
        if (method is not ("INSERT" or "UPDATE" or "DELETE" or "BATCH" or "BOOTSTRAP"))
        {
            await next(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
        // A batch may contain arbitrary keys from different groups; do not authorize
        // it using one key supplied separately in the query string.
        if (method is "BATCH" or "BOOTSTRAP") { context.Response.StatusCode = 403; return; }
        var email = context.User.FindFirstValue(ClaimTypes.Email);
        if (email == null) { context.Response.StatusCode = 403; return; }
        var factory = parts.Length == 4 ? parts[2] : string.Empty;
        var types = registry.GetByModel(typeof(Team)).Where(x => string.Equals(x.FactoryName ?? string.Empty, factory, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.KeyType).Distinct().ToArray();
        var key = context.Request.Query["key"];
        if (types.Length != 1 || key.Count != 1) { context.Response.StatusCode = 400; return; }
        string? groupId;
        try
        {
            var type = types[0];
            groupId = type == typeof(TeamKey) ? KeySettings<TeamKey>.Instance.Parse(key[0]!).Group
                : type == typeof(TeamDayKey) ? KeySettings<TeamDayKey>.Instance.Parse(key[0]!).Group
                : type == typeof(OpeningTeamKey) ? KeySettings<OpeningTeamKey>.Instance.Parse(key[0]!).Group
                : type == typeof(OpeningTeamDayKey) ? KeySettings<OpeningTeamDayKey>.Instance.Parse(key[0]!).Group
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IndexOutOfRangeException or System.Text.Json.JsonException)
        {
            context.Response.StatusCode = 400;
            return;
        }
        var group = groupId == null ? null : await groups.GetAsync(groupId, context.RequestAborted);
        if (group?.Users?.Any(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase) &&
            (x.Role.HasFlag(IdentityRole.Admin) || x.Role.HasFlag(IdentityRole.SuperAdmin))) != true)
        {
            context.Response.StatusCode = 403;
            return;
        }
        await next(context);
    }
}
