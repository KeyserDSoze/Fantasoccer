using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class RealPlayer
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("t")]
        public RealTeam RealTeam { get; set; }
        [JsonPropertyName("r")]
        public Role Role { get; set; }
        [JsonPropertyName("a")]
        public bool IsActive { get; set; }
        [JsonPropertyName("vh")]
        public bool Visible { get; set; } = true;
        [JsonIgnore]
        public string Key => KeyUtils.GetPlayerKey(Name);
    }
}
