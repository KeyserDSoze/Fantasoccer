using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeam
    {
        [JsonPropertyName("n")]
        public required string Name { get; set; }
        [JsonIgnore]
        public string Abbreviation => Name == null || Name.Length < 3 ? Name ?? string.Empty : Name.ToLower()[..3];
    }
}
