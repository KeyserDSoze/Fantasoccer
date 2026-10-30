using System.Security.Claims;

namespace Fantasoccer.Authentication;

// Social login contracts. The implementation (provider exchange, token endpoints) lives in
// Fantasoccer.Business: the application issues its own bearer/refresh tokens and uses the
// external provider only to learn who the user is.
public enum ProviderType
{
    // Refresh: the code is the application's own refresh token.
    DotNet = 0,
    Google = 1,
    Microsoft = 2
}

public interface ISocialUser
{
    string? Username { get; set; }
}

public sealed class SocialUser : ISocialUser
{
    public string? Username { get; set; }
    public static ISocialUser OnlyUsername(string? username) => new SocialUser { Username = username };
}

// Verified identity coming from the provider (or from a valid refresh token).
public sealed class TokenResponse
{
    public required string Username { get; set; }
    public required List<Claim> Claims { get; set; }
}

public interface ISocialUserProvider
{
    Task<ISocialUser> GetAsync(string username, IEnumerable<Claim> claims, CancellationToken cancellationToken);
    IAsyncEnumerable<Claim> GetClaimsAsync(TokenResponse response, CancellationToken cancellationToken);
}
