using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpAccountTools(IHttpContextAccessor context, IRepository<AppIdentity, string> users, FantasoccerSeasonContext seasons)
{
    [McpServerTool(Name = "get_my_account", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Start here: get your account, group IDs, saved selections and server current season. Follow groups[].i with api/Group/Get, then resolve league/basket/year/owner IDs from that document and use point reads. No generic repository queries are exposed. savedSelections may be historical. Identity is server-resolved; a display name/username is not an owner email. Read get_fantasoccer_guide(topic='lookups') for exact navigation.")]
    public async Task<object> GetMyAccountAsync(CancellationToken cancellationToken)
    {
        var email = context.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException();
        var account = await users.GetAsync(email, cancellationToken) ?? throw new UnauthorizedAccessException();
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
