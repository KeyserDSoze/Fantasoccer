using System.Security.Claims;
using RepositoryFramework;
using Rystem.Authentication.Social;

namespace Fantasoccer.Business.Authorization
{
    internal sealed class SocialUserProvider : ISocialUserProvider
    {
        private readonly IRepository<AppIdentity, string> _repository;
        public SocialUserProvider(IRepository<AppIdentity, string> repository)
        {
            _repository = repository;
        }

        public async Task<SocialUser> GetAsync(string username, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            var user = await _repository.GetAsync(username, cancellationToken);
            return user;
        }

        public async IAsyncEnumerable<Claim> GetClaimsAsync(string? username, CancellationToken cancellationToken)
        {
            var user = await _repository.GetAsync(username, cancellationToken);
            yield return new Claim(ClaimTypes.Name, user.Username);
            yield return new Claim(ClaimTypes.Email, user.Email);
        }
    }
}
