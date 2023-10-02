using Blazored.LocalStorage;
using Blazorise;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using RepositoryFramework;
using RepositoryFramework.Api.Client.DefaultInterceptor;

namespace Fantasoccer.Applications.App
{
    public class IdentityService
    {
        private readonly ILocalStorageService _localStorageService;
        private readonly NavigationManager _navigationManager;
        private readonly IRepository<AppIdentity, string> _appIdentity;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AuthenticatorSettings _settings;
        private readonly MicrosoftIdentityConsentAndConditionalAccessHandler _consentHandler;
        private readonly IAuthorizationHeaderProvider _authorizationHeaderProvider;

        public IdentityService(
            IHttpClientFactory httpClientFactory,
            AuthenticatorSettings settings,
            MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler,
            IAuthorizationHeaderProvider authorizationHeaderProvider,
            ILocalStorageService localStorageService,
            IConfiguration configuration,
            NavigationManager navigationManager,
            IRepository<AppIdentity, string> appIdentity,
            AuthenticationStateProvider authenticationStateProvider)
        {
            _httpClient = httpClientFactory.CreateClient("Identity");
            _settings = settings;
            _consentHandler = consentHandler;
            _authorizationHeaderProvider = authorizationHeaderProvider;
            _localStorageService = localStorageService;
            _configuration = configuration;
            _navigationManager = navigationManager;
            _appIdentity = appIdentity;
            _authenticationStateProvider = authenticationStateProvider;
        }
        public async Task<AppIdentity?> GetIdentityAsync()
        {
            var identity = await _localStorageService.GetItemAsync<IdentityLocalStorage>("id");
            if (identity != null && DateTime.UtcNow < identity.ExpiringTime)
            {
                return await _appIdentity.GetAsync(identity.Email);
            }
            else
            {
                await _localStorageService.RemoveItemAsync("id");
                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var email = user.Claims.First(x => x.Type == "emails").Value;
                    var token = await GetAccessTokenFromApiAsync();
                    await _localStorageService.SetItemAsync("id", new IdentityLocalStorage
                    {
                        ExpiringTime = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Options:ExpiringInMinutes"]!)),
                        AccessToken = token!,
                        Email = email
                    });
                    return await _appIdentity.GetAsync(email);
                }
                else
                    _navigationManager?.NavigateTo("../../MicrosoftIdentity/Account/SignIn", true);
            }
            return null;
        }
        private async Task<string?> GetAccessTokenFromApiAsync()
        {
            try
            {
                var token = await _authorizationHeaderProvider.CreateAuthorizationHeaderForUserAsync(_settings.Scopes!);
                var newToken = await _httpClient.GetFromJsonAsync<AuthenticationResponse>($"?token={token[7..].Trim()}");
                return newToken?.AccessToken;
            }
            catch (Exception ex)
            {
                _consentHandler.HandleException(ex);
            }
            return null;
        }
    }
}
