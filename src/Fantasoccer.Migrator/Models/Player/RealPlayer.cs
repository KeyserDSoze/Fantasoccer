using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record RealPlayer(string Name, string RealTeam, Role Role, int Year)
    {
        [JsonIgnore]
        public string Key => KeyUtils.GetPlayerKey(Year, Name);
        public bool IsActive { get; set; }
    }
}