using System.Text.Json.Serialization;
using Rystem.Authentication.Social;

namespace Fantasoccer
{
    public class AppIdentity : ISocialUser
    {
        [JsonPropertyName("u")]
        public string? Username { get; set; }
        [JsonPropertyName("e")]
        public string Email { get; set; }
        [JsonPropertyName("s")]
        public AppIdentityState State { get; set; } = new();
        [JsonPropertyName("g")]
        public List<GroupForIdentity> Groups { get; set; }
    }
}
