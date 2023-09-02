using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class RealPlayer
    {
        public string Name { get; set; }
        public RealTeam RealTeam { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
        [JsonIgnore]
        public string Key => KeyUtils.GetPlayerKey(Name);
    }
}
