using System.Text.Json.Serialization;

namespace Fantasoccer.Domain
{
    public sealed class AuthenticationResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [JsonPropertyName("expiration")]
        public DateTime ExpirationTime { get; set; }
        public static AuthenticationResponse Empty { get; } = new();
    }
}
