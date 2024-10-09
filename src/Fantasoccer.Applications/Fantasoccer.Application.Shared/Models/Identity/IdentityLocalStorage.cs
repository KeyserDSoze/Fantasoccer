using System.Text.Json.Serialization;

namespace Fantasoccer.Application.Shared
{
    public sealed class IdentityLocalStorage
    {
        public const string Key = "idkey";
        [JsonPropertyName("at")]
        public string AccessToken { get; set; }
        [JsonPropertyName("e")]
        public DateTime ExpiringTime { get; set; }
        [JsonPropertyName("u")]
        public string Email { get; set; }
    }
}
