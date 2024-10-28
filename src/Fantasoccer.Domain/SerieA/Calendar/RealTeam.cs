using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeam
    {
        [JsonPropertyName("n")]
        public required string Name { get; set; }
        private string? _abbreviation;
        [JsonPropertyName("a")]
        public string Abbreviation
        {
            get
            {
                return _abbreviation ??= Name == null || Name.Length < 3 ? Name ?? string.Empty : Name.ToLower()[..3];
            }
            set
            {
                _abbreviation = value;
            }
        }
    }
}
