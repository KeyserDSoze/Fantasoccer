using System.Security.Claims;
using RepositoryFramework;
using Rystem.Authentication.Social;
using Microsoft.IdentityModel.Tokens;

namespace Fantasoccer.Business.Authorization
{
    internal sealed class SocialUserProvider : ISocialUserProvider
    {
        private readonly IRepository<AppIdentity, string> _repository;
        private readonly SocialLoginBuilder _socialLogin;
        public SocialUserProvider(IRepository<AppIdentity, string> repository, SocialLoginBuilder socialLogin)
        {
            _repository = repository;
            _socialLogin = socialLogin;
        }

        public async Task<ISocialUser> GetAsync(string username, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            var email = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email);
            var user = await _repository.GetAsync(email.Value, cancellationToken);
            return user;
        }

        public async IAsyncEnumerable<Claim> GetClaimsAsync(TokenResponse response, CancellationToken cancellationToken)
        {
            // Rystem 10.0.8 verifies the Microsoft ID-token signature/lifetime but disables
            // issuer/audience checks. Enforce those on the verified provider claims before
            // issuing any application ticket. Internal refreshes only carry app claims.
            if (response.Claims.Any(x => x.Type is "iss" or "aud"))
            {
                const string personalAccountIssuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
                if (!response.Claims.Any(x => x.Type == "iss" && x.Value == personalAccountIssuer) ||
                    !response.Claims.Any(x => x.Type == "aud" && x.Value == _socialLogin.Microsoft.ClientId))
                    throw new SecurityTokenValidationException("Microsoft identity is not valid for this application.");
            }
#warning This is a temporary fix for the email address
            if (response.Username == "snail84muz@hotmail.com")
            {
                response.Username = "Snail84muz@hotmail.com";
            }
            var user = await _repository.GetAsync(response.Username, cancellationToken);
            if (user == null) throw new UnauthorizedAccessException("Application account not found.");
            // Display names are user-editable. Never derive a global privilege from Username.
            var isSuperAdmin = string.Equals(user.Email, "alessandro.rapiti44@gmail.com", StringComparison.OrdinalIgnoreCase);
            yield return new Claim(ClaimTypes.Name, user.Email);
            yield return new Claim(ClaimTypes.Email, user.Email);
            if (isSuperAdmin)
            {
                yield return new Claim(ClaimTypes.Role, "super_admin");
            }
        }
    }
}
