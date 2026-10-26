using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeam
    {
        [JsonPropertyName("n")]
        public required string Name { get; set; }
        private string? _abbreviation;
        private bool _hasExplicitAbbreviation;
        // Natural SQL identities must not mistake the legacy display fallback
        // below for an abbreviation actually supplied by the provider/source.
        [JsonIgnore]
        public string? ExplicitAbbreviation => _hasExplicitAbbreviation ? _abbreviation : null;
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
                _hasExplicitAbbreviation = true;
            }
        }
    }
}
