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

        public async Task<ISocialUser> GetAsync(string username, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            var email = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email);
            var user = await _repository.GetAsync(email.Value, cancellationToken);
            return user;
        }

        public async IAsyncEnumerable<Claim> GetClaimsAsync(string? username, CancellationToken cancellationToken)
        {
            var user = await _repository.GetAsync(username, cancellationToken);
            yield return new Claim(ClaimTypes.Name, user.Email);
            yield return new Claim(ClaimTypes.Email, user.Email);
        }
    }
}
