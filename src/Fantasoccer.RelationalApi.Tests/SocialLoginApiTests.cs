using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Fantasoccer.Authentication;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    private const string MicrosoftIssuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";

    private sealed class FixedKeys(SecurityKey key) : ISocialSigningKeyProvider
    {
        public Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(string metadataAddress, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SecurityKey>>([key]);
    }

    private sealed class FakeMicrosoft(Func<string> idToken) : HttpMessageHandler
    {
        public List<(string Origin, Dictionary<string, string> Form)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => Uri.UnescapeDataString(x[1].Replace('+', ' ')));
            Calls.Add((request.Headers.GetValues("Origin").Single(), form));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "provider-access", id_token = idToken() }) };
        }
    }

    [PostgreSqlFact]
    public async Task Social_login_verifies_the_provider_identity_issues_our_own_tokens_and_refreshes_for_a_year()
    {
        var email = $"Social-{Guid.NewGuid():N}@Example.Test";
        var clientId = factory.Services.GetRequiredService<IConfiguration>()["SocialLogin:Microsoft:ClientId"]!;
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test" };
        string IdToken(string? audience = null, string? issuer = null, SecurityKey? signingKey = null, DateTime? expires = null) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer ?? MicrosoftIssuer, audience ?? clientId,
                [new("preferred_username", email), new("sub", "subject-" + email), new("name", "Social Friend")],
                expires: (expires ?? DateTime.UtcNow.AddHours(1)).AddSeconds(0), notBefore: (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
                signingCredentials: new SigningCredentials(signingKey ?? key, SecurityAlgorithms.RsaSha256)));
        var current = IdToken();
        var microsoft = new FakeMicrosoft(() => current);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISocialSigningKeyProvider>(new FixedKeys(key));
            services.AddHttpClient("MicrosoftAuthenticationClient").ConfigurePrimaryHttpMessageHandler(() => microsoft);
        }));
        using var client = app.CreateClient();
        HttpRequestMessage Token(string query, string? origin = "http://localhost:3000", HttpContent? body = null)
        {
            var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, "/api/Authentication/Social/Token?" + query) { Content = body };
            if (origin is not null) request.Headers.Add("Origin", origin);
            return request;
        }

        // Authorization code (PKCE) -> our own bearer and refresh tokens, account created from the normalized email.
        var response = await client.SendAsync(Token("provider=2&code=the-code&redirectPath=/", body: JsonContent.Create(new { code_verifier = "my-verifier" })));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal("Bearer", tokens.GetProperty("tokenType").GetString());
        Assert.Equal(TimeSpan.FromHours(24).TotalSeconds, tokens.GetProperty("expiresIn").GetDouble());
        var refresh = tokens.GetProperty("refreshToken").GetString()!;
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("accessToken").GetString()));
        var call = Assert.Single(microsoft.Calls);
        Assert.Equal("http://localhost:3000/", call.Origin);
        Assert.Equal("the-code", call.Form["code"]); Assert.Equal("my-verifier", call.Form["code_verifier"]);
        Assert.Equal(clientId, call.Form["client_id"]); Assert.Equal("http://localhost:3000/", call.Form["redirect_uri"]);
        await using (var database = factory.Database.CreateContext())
        {
            var user = await database.Users.SingleAsync(x => x.Email == email.ToLowerInvariant());
            Assert.Equal("Social Friend", user.DisplayName);
            Assert.Single(await database.ExternalLogins.Where(x => x.UserId == user.Id).ToArrayAsync());
        }
        // The refresh token is ours and lasts one year.
        var ticket = app.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(refresh)!;
        Assert.InRange((ticket.Properties.ExpiresUtc!.Value - DateTimeOffset.UtcNow).TotalDays, 364.9, 365.1);
        Assert.Equal(email.ToLowerInvariant(), ticket.Principal.FindFirst(ClaimTypes.Email)!.Value);

        // Refresh: only from the origin it was issued to, never without one, never with garbage.
        var renewed = await client.SendAsync(Token("provider=0&code=" + Uri.EscapeDataString(refresh)));
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.NotEqual(refresh, (await renewed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Token("provider=0&code=" + Uri.EscapeDataString(refresh), origin: "https://fanta.zone"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Token("provider=0&code=" + Uri.EscapeDataString(refresh), origin: null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Token("provider=0&code=garbage"))).StatusCode);

        // The ID token must be ours: right audience, issuer, key and lifetime.
        using var other = RSA.Create(2048);
        foreach (var bad in new[] { IdToken(audience: "someone-else"), IdToken(issuer: "https://evil.example/v2.0"), IdToken(signingKey: new RsaSecurityKey(other) { KeyId = "test" }),
                     IdToken(expires: DateTime.UtcNow.AddHours(-1)) })
        {
            current = bad;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Token("provider=2&code=the-code&redirectPath=/"))).StatusCode);
        }
        // Origins not configured, unsupported providers and missing codes are refused.
        current = IdToken();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Token("provider=2&code=the-code&redirectPath=/", origin: "https://evil.example"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Token("provider=1&code=the-code&redirectPath=/"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Token("provider=2&code=&redirectPath=/"))).StatusCode);
    }
}
