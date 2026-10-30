using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class GroupForIdentity
    {
        [JsonPropertyName("i")]
        public required string Id { get; set; }
        [JsonPropertyName("n")]
        public required string Name { get; set; }
        [JsonPropertyName("r")]
        public required IdentityRole Role { get; set; }
    }
}
