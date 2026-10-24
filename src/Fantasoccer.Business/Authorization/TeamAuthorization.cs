using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using RepositoryFramework;
using RepositoryFramework.Api.Server.Authorization;

namespace Fantasoccer.Business.Authorization;

// These checks apply to public repository endpoints, not trusted repository calls
// made by formation, market or auction services. Ownership is NOT permission to
// replace a whole roster: players, prices and balances are administrative data.
internal abstract class TeamRepositoryAuthorization<TKey>(IRepository<Group, string> groups)
    : IRepositoryAuthorization<Team, TKey> where TKey : class
{
    protected abstract string GroupId(TKey key);

    public async Task<AuthorizedRepositoryResponse> HandleRequirementAsync(IHttpContextAccessor accessor,
        AuthorizationHandlerContext context, RepositoryRequirement requirement, RepositoryMethods method, TKey? key, Team? value)
    {
        if (key == null) return new() { Success = false };
        if (method is RepositoryMethods.Get or RepositoryMethods.Exist) return new() { Success = true };
        if (method is not (RepositoryMethods.Insert or RepositoryMethods.Update or RepositoryMethods.Delete))
            return new() { Success = false };
        var email = accessor.GetEmail();
        var group = await groups.GetAsync(GroupId(key), accessor.HttpContext?.RequestAborted ?? default);
        return new()
        {
            Success = email != null && group?.Users?.Any(x =>
                string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase) &&
                (x.Role.HasFlag(IdentityRole.Admin) || x.Role.HasFlag(IdentityRole.SuperAdmin))) == true
        };
    }
}

internal sealed class TeamAuthorization(IRepository<Group, string> groups) : TeamRepositoryAuthorization<TeamKey>(groups)
{
    protected override string GroupId(TeamKey key) => key.Group;
}

internal sealed class TeamDayAuthorization(IRepository<Group, string> groups) : TeamRepositoryAuthorization<TeamDayKey>(groups)
{
    protected override string GroupId(TeamDayKey key) => key.Group;
}

internal sealed class OpeningTeamAuthorization(IRepository<Group, string> groups) : TeamRepositoryAuthorization<OpeningTeamKey>(groups)
{
    protected override string GroupId(OpeningTeamKey key) => key.Group;
}

internal sealed class OpeningTeamDayAuthorization(IRepository<Group, string> groups) : TeamRepositoryAuthorization<OpeningTeamDayKey>(groups)
{
    protected override string GroupId(OpeningTeamDayKey key) => key.Group;
}
