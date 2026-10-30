using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class PlayedGame
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("h")]
        public string Home { get; set; }
        [JsonPropertyName("a")]
        public string Away { get; set; }
        [JsonPropertyName("r")]
        public GameResult Result { get; set; }
        [JsonPropertyName("1")]
        public List<Player> HomePlayers { get; set; }
        [JsonPropertyName("2")]
        public List<Player> AwayPlayers { get; set; }
    }
}
