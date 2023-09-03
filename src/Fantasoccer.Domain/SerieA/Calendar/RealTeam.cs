using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeam
    {
        public string Name { get; set; }
        [JsonIgnore]
        public string Abbreviation => Name.ToLower()[..3];
    }
}
