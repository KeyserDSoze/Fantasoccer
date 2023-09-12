using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Player : RealPlayer
    {
        [JsonPropertyName("p")]
        public int Price { get; set; }
        [JsonPropertyName("s")]
        public PlayerInTeamStatus Status { get; set; }
        [JsonPropertyName("k")]
        public FantaSoccerRole Position { get; set; }
    }
}
