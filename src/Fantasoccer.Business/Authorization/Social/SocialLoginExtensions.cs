using Fantasoccer.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace Microsoft.Extensions.DependencyInjection;

public static class SocialLoginExtensions
{
    private const string TokenRoute = "api/Authentication/Social/Token";
    private const string UserRoute = "api/Authentication/Social/User";

    // Registers the social login: the application's own bearer/refresh tokens and the provider exchanges.
    public static IServiceCollection AddSocialLogin<TProvider>(this IServiceCollection services, Action<SocialLoginBuilder> settings,
        Action<BearerTokenOptions>? bearer = null, ServiceLifetime userProviderLifetime = ServiceLifetime.Transient) where TProvider : class, ISocialUserProvider
    {
        services.AddAuthentication().AddBearerToken(bearer);
        services.Add(new ServiceDescriptor(typeof(ISocialUserProvider), typeof(TProvider), userProviderLifetime));
        var builder = new SocialLoginBuilder();
        settings(builder);
        services.AddSingleton(builder);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISocialSigningKeyProvider, OpenIdSigningKeyProvider>();
        services.AddKeyedTransient<ISocialCodeExchanger, RefreshTokenExchanger>(ProviderType.DotNet);
        services.AddKeyedTransient<ISocialCodeExchanger, MicrosoftCodeExchanger>(ProviderType.Microsoft);
        if (builder.Microsoft.IsActive)
            services.AddHttpClient(MicrosoftCodeExchanger.HttpClientName, client => client.BaseAddress = new Uri(MicrosoftCodeExchanger.TokenEndpoint));
        return services;
    }

    // POST/GET api/Authentication/Social/Token?provider=2&code=...&redirectPath=/ : exchanges the provider code (or, with
    // provider=0, an application refresh token) and answers with the application's own bearer and refresh tokens.
    // GET api/Authentication/Social/User : the signed-in application user.
    public static IEndpointRouteBuilder MapSocialLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(TokenRoute, async (HttpContext context, [FromServices] ISocialUserProvider users,
            [FromServices] ILoggerFactory loggers, [FromQuery] ProviderType provider, [FromQuery] string code, [FromQuery] string? redirectPath = null,
            [FromBody] Dictionary<string, string>? additionalParameters = null, CancellationToken cancellationToken = default) =>
        {
            var logger = loggers.CreateLogger("Fantasoccer.Authentication");
            var exchanger = context.RequestServices.GetKeyedService<ISocialCodeExchanger>(provider);
            var domain = RequestOrigin(context.Request);
            if (exchanger is null || string.IsNullOrWhiteSpace(domain)) return Results.Unauthorized();
            SocialExchangeResult result;
            try { result = await exchanger.ExchangeAsync(code, new(domain, redirectPath, additionalParameters), cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Token acquisition error.");
                return Results.Problem("Token acquisition failed.");
            }
            if (result.Error is not null) return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
            if (result.Token is null || string.IsNullOrWhiteSpace(result.Token.Username)) return Results.Unauthorized();
            var claims = new List<Claim>();
            await foreach (var claim in users.GetClaimsAsync(result.Token, cancellationToken)) claims.Add(claim);
            claims.Add(new Claim(SocialClaimTypes.Domain, domain));
            return Results.SignIn(new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
        }).WithName("/Social/Token").WithDisplayName("/Social/Token").WithGroupName("Social")
          .WithDescription("Get the application token from a social login (PKCE) or from a refresh token.");
        endpoints.Map(UserRoute, async (HttpContext context, [FromServices] ISocialUserProvider users, [FromServices] ILoggerFactory loggers, CancellationToken cancellationToken) =>
        {
            if (context.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            try { return Results.Json<ISocialUser>(await users.GetAsync(context.User.Identity.Name ?? string.Empty, context.User.Claims, cancellationToken)); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                loggers.CreateLogger("Fantasoccer.Authentication").LogError(exception, "Error getting user.");
                return Results.Unauthorized();
            }
        }).WithName("/Social/User").WithDisplayName("/Social/User").WithGroupName("Social")
          .WithDescription("Get the signed-in user.").RequireAuthorization();
        return endpoints;
    }

    // The frontend origin: Origin header, else the origin of the Referer.
    private static string RequestOrigin(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out var origin) && !StringValues.IsNullOrEmpty(origin)) return origin.ToString().Trim('/');
        if (request.Headers.TryGetValue("Referer", out var referer) && Uri.TryCreate(referer.ToString(), UriKind.Absolute, out var uri)) return uri.GetLeftPart(UriPartial.Authority);
        return string.Empty;
    }
}
