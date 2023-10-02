using Blazored.LocalStorage;
using RepositoryFramework.Api.Client.Authorization;

namespace Fantasoccer.Applications.App
{
    internal sealed class TokenManager : ITokenManager
    {
        private readonly ILocalStorageService _localStorageService;
        public TokenManager(ILocalStorageService localStorageService)
        {
            _localStorageService = localStorageService;
        }
        public async Task EnrichWithAuthorizationAsync(HttpClient client)
        {
            var tokenResponse = await Try.WithDefaultOnCatchAsync(() => GetTokenAsync()).NoContext();
            if (tokenResponse?.Exception == null && tokenResponse?.Entity != null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse);
            }
        }
        public Task<string?> GetTokenAsync()
            => RefreshTokenAsync();

        public async Task<string?> RefreshTokenAsync()
        {
            var accessToken = await _localStorageService.GetItemAsync<IdentityLocalStorage>("id");
            return accessToken?.AccessToken;
        }
    }
}
