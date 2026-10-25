using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

// Additional resource checks for the new MCP surface. The existing endpoint policies,
// repository authorization and business hooks are still executed by McpApiFacade.
public sealed class McpAccessGuard(IRepository<Group, string> groups, IRepository<AppIdentity, string> users)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> GlobalReadRepositories = new(StringComparer.OrdinalIgnoreCase)
    {
        "RealCalendar", "RealRank", "RealPlayersWrapper", "RealTeamWrapper", "StatPlayersWrapper",
        "ChancedRealPlayerWrapper", "VotedRealPlayerWrapper", "CardWrapper"
    };

    public async Task<bool> AllowsAsync(McpOperation operation, IDictionary<string, JsonElement> arguments,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var email = user.FindFirstValue(ClaimTypes.Email);
        if (email == null) return false;
        if (user.IsInRole("super_admin")) return true;
        if (await users.GetAsync(email, cancellationToken) == null) return false;
        var path = operation.Api.RelativePath!.Split('?')[0];
        var parts = path.Split('/');
        var query = arguments.TryGetValue("query", out var value) && value.ValueKind == JsonValueKind.Object ? value : default;
        var body = arguments.TryGetValue("body", out var payload) ? payload : default;

        if (operation.Api.ActionDescriptor is not ControllerActionDescriptor && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 3) return false;
            var repository = parts[1];
            var method = parts[^1];
            // Bulk queries and batches can span arbitrary groups. Never authorize them by one supplied key.
            if (method is "Batch" or "Bootstrap") return false;
            if (repository == "AppIdentity") return await IdentityAsync(method, query, body, email, cancellationToken);
            if (repository == "Settings") return Same(Text(query, "key"), email) && method is "Get" or "Exist" or "Update" or "Insert";
            if (GlobalReadRepositories.Contains(repository)) return operation.ReadOnly;
            if (method is "Query" or "Count") return false;

            var keyText = Text(query, "key");
            if (keyText == null) return false;
            if (repository == "Group")
            {
                var group = await groups.GetAsync(keyText, cancellationToken);
                if (group == null || !Member(group, email)) return false;
                if (operation.ReadOnly) return true;
                if (!Admin(group, email)) return false;
                // Group admins cannot appoint/demote super-admins or remove their own authorization anchor.
                if (method == "Delete") return SuperAdmin(group, email);
                var replacement = body.ValueKind == JsonValueKind.Object ? body.Deserialize<Group>(Json) : null;
                return replacement != null && replacement.Id == group.Id && SafeRoles(group, replacement, email);
            }
            if (repository == "LiveGroup")
            {
                var group = await groups.GetAsync(keyText, cancellationToken);
                return group != null && Member(group, email) && operation.ReadOnly;
            }
            JsonElement key;
            try { key = JsonSerializer.Deserialize<JsonElement>(keyText); }
            catch (JsonException) { return false; }
            var groupId = Text(key, "g", "Group");
            if (groupId == null) return false; // Unknown/global repositories fail closed for non-global admins.
            var targetGroup = await groups.GetAsync(groupId, cancellationToken);
            if (targetGroup == null || !Member(targetGroup, email)) return false;
            var leagueId = Text(key, "l", "League");
            if (leagueId != null && targetGroup.Leagues?.Any(x => x.Id == leagueId) != true) return false;
            if (operation.ReadOnly) return true;
            if (repository.StartsWith("Team", StringComparison.OrdinalIgnoreCase) || repository.StartsWith("OpeningTeam", StringComparison.OrdinalIgnoreCase))
            {
                // Only the formation/market commands may write on behalf of an owner.
                // A generic Team replacement could otherwise sell/remove/add players,
                // alter their prices or forge the balance while retaining ownership.
                if (!Admin(targetGroup, email)) return false;
                var owner = Text(key, "e", "Email");
                var basket = Text(key, "b", "BasketId");
                var year = Number(key, "y", "Year");
                if (year == null || owner == null || basket == null) return false;
                var annual = targetGroup.Baskets?.FirstOrDefault(x => x.Id == basket)?.Years?
                    .FirstOrDefault(x => x.Year == year)?.Teams?.FirstOrDefault(x => Same(x.Owner, owner));
                if (annual == null || (!annual.IsOwner(email) && !Admin(targetGroup, email))) return false;
                if (method == "Delete") return Admin(targetGroup, email);
                var team = body.ValueKind == JsonValueKind.Object ? body.Deserialize<Team>(Json) : null;
                return team != null && Same(team.Owner, annual.Owner) &&
                    (team.AdditionalOwners ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    .SetEquals(annual.AdditionalOwners ?? []);
            }
            return Admin(targetGroup, email);
        }

        // Controllers keep their own model binding and resource checks; close the missing group boundaries here.
        var controller = (operation.Api.ActionDescriptor as ControllerActionDescriptor)?.ControllerName ?? parts[0];
        if (controller == "Context") return operation.ReadOnly;
        if (controller is "Recalculation") return false;
        if (controller is "Card") return operation.ReadOnly;
        if (controller is "Ping" or "PushNotification" or "McpAuction") return true;
        if (controller == "Auction")
            return true; // Creation and lookup resolve membership in the existing controller.
        var groupParameter = Text(query, "groupId");
        if (groupParameter != null)
        {
            var group = await groups.GetAsync(groupParameter, cancellationToken);
            if (group == null || !Member(group, email)) return false;
            var leagueParameter = Text(query, "leagueId");
            if (leagueParameter != null && group.Leagues?.Any(x => x.Id == leagueParameter) != true) return false;
            if (controller == "LeagueManager") return parts[^1] == "Recalculate" ? SuperAdmin(group, email) : Admin(group, email);
            return true;
        }
        if (controller == "Settings") return true;
        if (controller == "PlayerImage") return true; // Its controller enforces ownership and membership.
        return false;
    }

    private async Task<bool> IdentityAsync(string method, JsonElement query, JsonElement body, string email, CancellationToken cancellationToken)
    {
        var key = Text(query, "key");
        // The existing group-admin UI looks up accounts by email and provisions invitees.
        // Preserve that workflow, but verify admin roles against Group.Users, not editable profile claims.
        if (method == "Query") return await HasAnyAdminGroupAsync(email, cancellationToken);
        if (key == null) return false;
        if (method is "Get" or "Exist") return Same(key, email) || await HasAnyAdminGroupAsync(email, cancellationToken);
        if (method is not ("Update" or "Insert") || body.ValueKind != JsonValueKind.Object) return false;
        var current = await users.GetAsync(key, cancellationToken);
        var replacement = body.Deserialize<AppIdentity>(Json);
        if (replacement == null || !Same(key, replacement.Email)) return false;
        if (method == "Insert" && (current != null || replacement.Groups is not { Count: > 0 })) return false;
        if (method == "Update" && current == null) return false;
        if (current != null && !Same(key, email) &&
            (replacement.Username != current.Username || JsonSerializer.Serialize(current.State) != JsonSerializer.Serialize(replacement.State))) return false;
        var previous = (current?.Groups ?? []).ToDictionary(x => x.Id);
        var next = (replacement.Groups ?? []).ToDictionary(x => x.Id);
        foreach (var id in previous.Keys.Union(next.Keys))
        {
            if (JsonSerializer.Serialize(previous.GetValueOrDefault(id)) == JsonSerializer.Serialize(next.GetValueOrDefault(id))) continue;
            var group = await groups.GetAsync(id, cancellationToken);
            if (group == null || !Admin(group, email)) return false;
            if (!SuperAdmin(group, email) &&
                ((previous.GetValueOrDefault(id)?.Role ?? 0).HasFlag(IdentityRole.SuperAdmin) ||
                 (next.GetValueOrDefault(id)?.Role ?? 0).HasFlag(IdentityRole.SuperAdmin))) return false;
        }
        return true;
    }

    private async Task<bool> HasAnyAdminGroupAsync(string email, CancellationToken cancellationToken)
    {
        var account = await users.GetAsync(email, cancellationToken);
        foreach (var candidate in account?.Groups ?? [])
        {
            var group = await groups.GetAsync(candidate.Id, cancellationToken);
            if (group != null && Admin(group, email)) return true;
        }
        return false;
    }

    private static bool SafeRoles(Group previous, Group next, string email)
    {
        if (SuperAdmin(previous, email)) return true;
        var oldSuper = previous.Users?.Where(x => x.Role.HasFlag(IdentityRole.SuperAdmin)).Select(x => x.Email).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var newSuper = next.Users?.Where(x => x.Role.HasFlag(IdentityRole.SuperAdmin)).Select(x => x.Email).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        return oldSuper.SetEquals(newSuper) && Admin(next, email);
    }

    private static bool Member(Group group, string email) => group.Users?.Any(x => Same(x.Email, email) && x.Role != IdentityRole.None) == true;
    private static bool Admin(Group group, string email) => group.Users?.Any(x => Same(x.Email, email) && (x.Role.HasFlag(IdentityRole.Admin) || x.Role.HasFlag(IdentityRole.SuperAdmin))) == true;
    private static bool SuperAdmin(Group group, string email) => group.Users?.Any(x => Same(x.Email, email) && x.Role.HasFlag(IdentityRole.SuperAdmin)) == true;
    private static bool Same(string? left, string? right) => left != null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static string? Text(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        string? result = null;
        foreach (var property in element.EnumerateObject())
            if (names.Any(x => x.Equals(property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                if (result != null && result != value) return null; // Reject conflicting aliases/duplicate keys.
                result = value;
            }
        return result;
    }
    private static int? Number(JsonElement element, params string[] names) => int.TryParse(Text(element, names), out var value) ? value : null;
}
