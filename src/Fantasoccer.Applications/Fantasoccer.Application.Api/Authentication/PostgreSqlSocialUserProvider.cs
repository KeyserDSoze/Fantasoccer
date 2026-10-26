using System.Security.Claims;
using System.Text.Json.Serialization;
using Fantasoccer.Application.Core.Accounts;
using Microsoft.IdentityModel.Tokens;
using Rystem.Authentication.Social;

namespace Fantasoccer.Application.Api.Authentication;

public sealed class PostgreSqlSocialUserProvider(AccountApplicationService accounts, SocialLoginBuilder login) : ISocialUserProvider
{
    public async Task<ISocialUser> GetAsync(string username, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        var email = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
        var account = email is null ? null : await accounts.FindForLoginAsync(email, cancellationToken);
        return account is null ? throw new UnauthorizedAccessException("Application account unavailable.") : new RelationalSocialProfile(account);
    }

    public async IAsyncEnumerable<Claim> GetClaimsAsync(TokenResponse response, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AccountIdentityDto? account = null;
        if (response.Claims.Any(x => x.Type is "iss" or "aud"))
        {
            const string issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
            if (!response.Claims.Any(x => x.Type == "iss" && x.Value == issuer) ||
                !response.Claims.Any(x => x.Type == "aud" && x.Value == login.Microsoft.ClientId))
                throw new SecurityTokenValidationException("Microsoft identity is not valid for this application.");
            var subject = response.Claims.FirstOrDefault(x => x.Type == "sub")?.Value
                ?? response.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            if (subject is null) throw new SecurityTokenValidationException("Microsoft subject is missing.");
            var displayName = response.Claims.FirstOrDefault(x => x.Type is ClaimTypes.Name or "name")?.Value;
            account = await accounts.ResolveExternalLoginAsync(response.Username, issuer, login.Microsoft.ClientId!, subject, cancellationToken, displayName);
        }
        else account = await accounts.FindForLoginAsync(response.Username, cancellationToken);
        if (account is null) throw new UnauthorizedAccessException("Application account unavailable.");
        yield return new(ClaimTypes.NameIdentifier, account.Id.ToString());
        yield return new(ClaimTypes.Name, account.Email);
        yield return new(ClaimTypes.Email, account.Email);
        if (accounts.IsGlobalAdministrator(account.Email)) yield return new(ClaimTypes.Role, "super_admin");
    }
}

public sealed class RelationalSocialProfile(AccountIdentityDto account) : ISocialUser
{
    [JsonPropertyName("id")] public Guid Id => account.Id;
    [JsonPropertyName("username")] public string? Username { get; set; } = account.Username;
    [JsonPropertyName("email")] public string Email => account.Email;
    [JsonPropertyName("state")] public AccountStateDto State => account.State;
    [JsonPropertyName("groups")] public IdentityGroupDto[] Groups => account.Groups;
}
