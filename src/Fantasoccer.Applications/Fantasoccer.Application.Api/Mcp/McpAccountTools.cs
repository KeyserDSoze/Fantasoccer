using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpAccountTools(IHttpContextAccessor context, FantasoccerSeasonContext seasons, IServiceProvider services)
{
    [McpServerTool(Name = "get_my_account", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Start here: get the authenticated account, memberships, saved selections and current season. PostgreSQL returns readable fields and UUIDs for /api/groups; do not use them as legacy repository keys. Saved selections are not permissions. Consult the guide for the active backend.")]
    public async Task<object> GetMyAccountAsync(CancellationToken cancellationToken)
    {
        var email = context.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException();
        if (services.GetService<Fantasoccer.Application.Core.Accounts.AccountApplicationService>() is { } relational)
        {
            var profile = await relational.CurrentAsync(context.HttpContext!.User, cancellationToken);
            return new { profile.Id, profile.Email, profile.Username, profile.Groups, profile.State,
                currentSeason = seasons.GetCurrent().CurrentSeason,
                nextRead = new { path = "api/workspace/groups", note = "Use readable groupId values; selection does not grant authorization." } };
        }
        var account = await services.GetRequiredService<IRepository<AppIdentity, string>>().GetAsync(email, cancellationToken) ?? throw new UnauthorizedAccessException();
        var season = seasons.GetCurrent();
        return new
        {
            account.Email, account.Username, account.Groups, account.State,
            // Preserve the old fields; add readable context rather than rewriting payloads.
            savedSelections = new
            {
                account.State.GroupId, account.State.LeagueId, seasonIndex = account.State.Year,
                seasonLabel = account.State.Year is >= 1 and <= 7987 ? FantasoccerSeasonContext.Describe(account.State.Year.Value).Label : null,
                selectedTeamOwnerEmail = account.State.MeOrLeaderEmail,
                note = "Saved UI selections, not authorization and not necessarily the current season."
            },
            currentSeason = season.CurrentSeason, serverTimeUtc = season.ServerTimeUtc,
            nextRead = new
            {
                path = "api/Group/Get", keySource = "groups[].i", guideTopic = "lookups",
                note = "Read the selected group by its simple ID. Use its leagues, annual baskets/teams and owner emails as the lookup index for subsequent Get/Exist calls; do not search all documents."
            }
        };
    }
}
