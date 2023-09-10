using System.IdentityModel.Tokens.Jwt;
using Fantasoccer.Domain;
using Fantasoccer.Domain.SerieA;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using RepositoryFramework.Api.Client.Authorization;
using RepositoryFramework.Api.Client.DefaultInterceptor;

namespace Fantasoccer.Applications.App.Extensions
{
    public sealed record SuperFastLog(string CorrelationId, string Message)
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public static List<SuperFastLog> Logs { get; } = new();
        public static void Add(string correlationId, string message)
            => Logs.Add(new(correlationId, message));
    }

    internal sealed class TokenManager : ITokenManager
    {

        private readonly AuthenticatorSettings _settings;
        private readonly MicrosoftIdentityConsentAndConditionalAccessHandler _consentHandler;
        private readonly IAuthorizationHeaderProvider _authorizationHeaderProvider;

        public TokenManager(
            AuthenticatorSettings settings,
            MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler,
            IAuthorizationHeaderProvider authorizationHeaderProvider)
        {
            _settings = settings;
            _consentHandler = consentHandler;
            _authorizationHeaderProvider = authorizationHeaderProvider;
        }
        public async Task EnrichWithAuthorizationAsync(HttpClient client)
        {
            var tokenResponse = await Try.WithDefaultOnCatchAsync(() => GetTokenAsync()).NoContext();
            var id = Guid.NewGuid().ToString();
            if (tokenResponse?.Exception == null && tokenResponse?.Entity != null)
            {
                var splitted = tokenResponse.Entity.Split(' ');
                var token = new JwtSecurityTokenHandler().ReadJwtToken(splitted[1]);
                var emails = token.Claims.First(x => x.Type == "emails");
                SuperFastLog.Add(id, $"Gathered email: {emails}");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(splitted[0], splitted[1]);
            }
        }
        public Task<string?> GetTokenAsync()
        {
            return RefreshTokenAsync();
        }
        public async Task<string?> RefreshTokenAsync()
        {
            try
            {
                var token = await _authorizationHeaderProvider.CreateAuthorizationHeaderForUserAsync(_settings.Scopes!);
                return token;
            }
            catch (Exception ex)
            {
                _consentHandler.HandleException(ex);
            }
            return null;
        }
    }
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpClientIntegrationForApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClientIntegration<RealCalendar, int>(configuration);
            services.AddHttpClientIntegration<RealPlayersWrapper, int>(configuration);
            services.AddHttpClientIntegration<ChancedRealPlayerWrapper, YearDayKey>(configuration);
            services.AddOrOverrideService<ITokenManager, TokenManager>(ServiceLifetime.Transient);
            services.AddRepository<VotedRealPlayerWrapper, YearDayKey>(builder =>
            {
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    apiBuilder.WithServerFactoryName("live");
                }, "live");
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    apiBuilder.WithServerFactoryName("official");
                }, "official");
            });
            services.AddHttpClientIntegration<Group, string>(configuration);
            services.AddHttpClientIntegration<Rank, LeagueKey>(configuration);
            services.AddHttpClientIntegration<Calendar, LeagueKey>(configuration);
            services.AddHttpClientIntegration<PlayedGame, GameKey>(configuration);
            services.AddHttpClientIntegration<SerieATeamsWrapper, int>(configuration);
            services.AddHttpClientIntegration<StatPlayersWrapper, int>(configuration);
            services.AddHttpClientIntegration<Team, TeamKey>(configuration);
            services.AddHttpClientIntegration<Team, TeamDayKey>(configuration, "day");
            services.AddHttpClientIntegration<AppIdentity, string>(configuration);
            return services;
        }
        private static IServiceCollection AddHttpClientIntegration<T, TKey>(this IServiceCollection services,
            IConfiguration configuration,
            string? factoryName = null)
           where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder =>
            {
                builder.WithApiClient(apiBuilder =>
                {
                    apiBuilder
                        .WithHttpClient(configuration["Api:Uri"]!)
                        .WithDefaultRetryPolicy();
                    if (factoryName != null)
                        apiBuilder.WithServerFactoryName(factoryName);
                }, factoryName, ServiceLifetime.Transient);
            });
        }
    }
}
