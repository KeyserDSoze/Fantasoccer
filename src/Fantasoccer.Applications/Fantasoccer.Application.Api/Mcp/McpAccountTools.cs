using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Mcp;

[McpServerToolType]
public sealed class McpAccountTools(IHttpContextAccessor context, IRepository<AppIdentity, string> users, FantasoccerSeasonContext seasons)
{
    [McpServerTool(Name = "get_my_account", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get your authenticated account, group IDs, saved selections and server current season. savedSelections expands the compact account.State; its year is a season index and may be historical, not the live season. Identity is server-resolved. Use the payload/model guide rather than guessing abbreviated fields.")]
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
            currentSeason = season.CurrentSeason, serverTimeUtc = season.ServerTimeUtc
        };
    }
}
