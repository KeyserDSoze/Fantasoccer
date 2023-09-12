using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class UserOfAGroup
    {
        [JsonPropertyName("u")]
        public string Username { get; set; }
        [JsonPropertyName("e")]
        public string Email { get; set; }
        [JsonPropertyName("r")]
        public IdentityRole Role { get; set; } 
    }
}
