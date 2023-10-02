using System.Text.Json.Serialization;

namespace Fantasoccer.Applications.App
{
    public class IdentityLocalStorage
    {
        [JsonPropertyName("at")]
        public string AccessToken { get; set; }
        [JsonPropertyName("e")]
        public DateTime ExpiringTime { get; set; }
        [JsonPropertyName("u")]
        public string Email { get; set; }
    }
}
