using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using RepositoryFramework;
using RepositoryFramework.Api.Server.Authorization;

namespace Fantasoccer.Business.Authorization
{
    internal sealed class TeamAuthorization : IRepositoryAuthorization<Team, TeamKey>
    {
        private static readonly AuthorizedRepositoryResponse s_notAuthorized = new() { Success = false };
        private static readonly AuthorizedRepositoryResponse s_authorized = new() { Success = true };
        public Task<AuthorizedRepositoryResponse> HandleRequirementAsync(IHttpContextAccessor httpContextAccessor,
            AuthorizationHandlerContext context,
            RepositoryRequirement requirement,
            RepositoryMethods method,
            TeamKey? key,
            Team? value)
        {
            if (method == RepositoryMethods.Delete)
                return Task.FromResult(s_notAuthorized);
            if (method == RepositoryMethods.Insert || method == RepositoryMethods.Update)
            {
                var email = httpContextAccessor.GetEmail();
                if (email != null && value?.Owner == email)
                {
                    return Task.FromResult(s_authorized);
                }
            }
            else if (method == RepositoryMethods.Get || method == RepositoryMethods.Exist)
            {
                return Task.FromResult(s_authorized);
            }
            return Task.FromResult(s_notAuthorized);
        }
    }
}
