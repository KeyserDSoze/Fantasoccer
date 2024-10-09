using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]/[action]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly int _expiringInMinutes;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IdentityOptions _identityOptions;
        private readonly SecurityOptions _securityOptions;
        private readonly string _issuerUri = string.Empty;
        public AuthenticationController(
            IRepository<AppIdentity, string> identityRepository,
            IdentityOptions identityOptions,
            SecurityOptions securityOptions)
        {
            _expiringInMinutes = identityOptions.ExpiringInMinutes;
            _identityRepository = identityRepository;
            _identityOptions = identityOptions;
            _securityOptions = securityOptions;
        }
        [HttpGet]
        public async Task<AuthenticationResponse> TokenAsync([FromQuery] string token)
        {
            //var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ip = string.Empty;
            if (ip != null)
            {
                var expiration = DateTime.UtcNow.AddMinutes(_expiringInMinutes);
                var email = await CheckTokenIsValidAndReturnEmailAsync(token);
                var user = await _identityRepository.GetAsync(email);
                if (user == null)
                {
                    user = new AppIdentity
                    {
                        Email = email,
                        Groups = new(),
                        State = new(),
                        Username = email
                    };
                    await _identityRepository.InsertAsync(email, user);
                }
                List<Claim> claims = new()
                    {
                        new Claim(ClaimTypes.Name, user!.Username),
                        new Claim(ClaimTypes.Email, user!.Email)
                    };
                var hash = CreateHashClaim(user.Email, expiration);
                claims.Add(new Claim(ClaimTypes.Hash, hash));
                return CreateToken(claims, expiration);
            }
            return AuthenticationResponse.Empty;
        }
        [HttpPost]
        public AuthenticationResponse? Refresh([FromBody] AuthenticationResponse request)
        {
            //var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? TestIp;
            var ip = string.Empty;
            if (ip != null)
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var parameters = _securityOptions.Rsa.ToParameters();
                var rsa = new RSACryptoServiceProvider(2048);
                rsa.ImportParameters(parameters);
                RsaSecurityKey rsaSecurityKey = new(rsa);
                if (request.AccessToken != null)
                {
                    var token = Try.WithDefaultOnCatch(
                        () =>
                        {
                            tokenHandler.ValidateToken(request.RefreshToken, new TokenValidationParameters
                            {
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = rsaSecurityKey,
                                ValidateIssuer = true,
                                ValidIssuer = _issuerUri,
                                ValidAudience = _issuerUri,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.FromMinutes(10)
                            }, out var validatedToken);

                            var jwtToken = (JwtSecurityToken)validatedToken;
                            var hashToCheck = jwtToken.Claims.First(x => x.Type == ClaimTypes.Hash).Value;
                            var username = jwtToken.Claims.First(x => x.Type == ClaimTypes.Name).Value;
                            var hashCalculationToCheck = CreateHashClaim(username, jwtToken.ValidTo.AddMinutes(-_expiringInMinutes));
                            if (hashToCheck == hashCalculationToCheck)
                            {
                                var expiration = DateTime.UtcNow.AddMinutes(_expiringInMinutes);
                                List<Claim> claims = new();
                                foreach (var claim in jwtToken.Claims)
                                    if (claim.Type == ClaimTypes.Name || claim.Type == ClaimTypes.Role)
                                        claims.Add(claim);
                                var hash = CreateHashClaim(username, expiration);
                                claims.Add(new Claim(ClaimTypes.Hash, hash));
                                return CreateToken(claims, expiration);
                            }
                            return AuthenticationResponse.Empty;
                        });
                    return token.Entity ?? AuthenticationResponse.Empty;
                }
            }
            return AuthenticationResponse.Empty;
        }
        private async Task<string> CheckTokenIsValidAndReturnEmailAsync(string access_token)
        {
            var stsDiscoveryEndpoint = $"{_identityOptions.Instance}/{_identityOptions.Domain}/{_identityOptions.Policy}/v2.0/.well-known/openid-configuration";
            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever());
            try
            {
                var config = await configManager.GetConfigurationAsync();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidAudience = _identityOptions.Audience,
                    IssuerSigningKeys = config.SigningKeys,
                    ValidIssuer = config.Issuer
                };
                var tokendHandler = new JwtSecurityTokenHandler();
                var result = tokendHandler.ValidateToken(access_token, validationParameters, out var jwt);
                if (result?.Identity != null && jwt is JwtSecurityToken dataToValidate)
                    return dataToValidate.Claims.First(x => x.Type == "emails").Value.Trim(']').Trim('[').Trim('"');
            }
            catch (Exception)
            {
                return string.Empty;
            }
            return string.Empty;
        }
        private string CreateHashClaim(string username, DateTime expiration)
            => $"{HttpContext.Connection.RemoteIpAddress}_{username}_{expiration.AddMinutes(_expiringInMinutes)}".ToHash();
        private AuthenticationResponse CreateToken(IEnumerable<Claim> claims, DateTime expiration)
        {
            var parameters = _securityOptions.Rsa.ToParameters();
            var rsa = new RSACryptoServiceProvider(2048);
            rsa.ImportParameters(parameters);
            RsaSecurityKey rsaSecurityKey = new(rsa);
            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken
            (
                _issuerUri,
                _issuerUri,
                claims,
                DateTime.UtcNow.AddMinutes(-_expiringInMinutes),
                expiration,
                new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256)
            );
            var accessToken = handler.WriteToken(token);
            var handlerForRefresh = new JwtSecurityTokenHandler();
            var tokenForRefresh = new JwtSecurityToken
            (
                _issuerUri,
                _issuerUri,
                claims,
                expiration.AddMinutes(_expiringInMinutes - 10),
                expiration.AddMinutes(_expiringInMinutes),
                new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256)
            );
            var refreshToken = handlerForRefresh.WriteToken(tokenForRefresh);
            return new AuthenticationResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpirationTime = expiration
            };
        }
    }
}
