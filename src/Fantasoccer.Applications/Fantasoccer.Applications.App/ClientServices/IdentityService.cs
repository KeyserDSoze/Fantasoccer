using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using RepositoryFramework.Api.Client.DefaultInterceptor;

namespace Fantasoccer.Applications.App
{
    public class IdentityService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthenticatorSettings _settings;
        private readonly MicrosoftIdentityConsentAndConditionalAccessHandler _consentHandler;
        private readonly IAuthorizationHeaderProvider _authorizationHeaderProvider;
        public IdentityService(
            IHttpClientFactory httpClientFactory,
            AuthenticatorSettings settings,
           MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler,
           IAuthorizationHeaderProvider authorizationHeaderProvider)
        {
            _httpClient = httpClientFactory.CreateClient("Identity");
            _settings = settings;
            _consentHandler = consentHandler;
            _authorizationHeaderProvider = authorizationHeaderProvider;
        }
        public async Task<string?> GetTokenAsync()
        {
            try
            {
                var token = await _authorizationHeaderProvider.CreateAuthorizationHeaderForUserAsync(_settings.Scopes!);
                var newToken = await _httpClient.GetStringAsync($"?token={token}");
                return newToken;
            }
            catch (Exception ex)
            {
                _consentHandler.HandleException(ex);
            }
            return null;
        }
    }
}
