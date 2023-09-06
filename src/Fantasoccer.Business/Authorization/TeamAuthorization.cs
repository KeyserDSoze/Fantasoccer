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
        private readonly IRepository<Group, string> _groupRepository;
        public TeamAuthorization(IRepository<Group, string> groupRepository)
        {
            _groupRepository = groupRepository;
        }
        public async Task<AuthorizedRepositoryResponse> HandleRequirementAsync(IHttpContextAccessor httpContextAccessor,
            AuthorizationHandlerContext context,
            RepositoryRequirement requirement,
            RepositoryMethods method,
            TeamKey? key,
            Team? value)
        {
            if (key == null)
                return s_notAuthorized;
            if (method == RepositoryMethods.Insert || method == RepositoryMethods.Update || method == RepositoryMethods.Delete)
            {
                var email = httpContextAccessor.GetEmail();
                if (email != null && value?.Owner == email)
                {
                    return s_authorized;
                }
                else
                {
                    var group = await _groupRepository.GetAsync(key.Group);
                    if (group?.AdminUsers.Any(x => x.Email == email) == true)
                    {
                        return s_authorized;
                    }
                }
            }
            else if (method == RepositoryMethods.Get || method == RepositoryMethods.Exist)
            {
                return s_authorized;
            }
            return s_notAuthorized;
        }
    }
}
