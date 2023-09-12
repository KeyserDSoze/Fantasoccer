using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class AppIdentity
    {
        [JsonPropertyName("u")]
        public string Username { get; set; }
        [JsonPropertyName("e")]
        public string Email { get; set; }
        [JsonPropertyName("s")]
        public AppIdentityState State { get; set; } = new();
        [JsonPropertyName("g")]
        public List<GroupForIdentity> Groups { get; set; }
    }
}
