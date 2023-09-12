using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class CalendarGame
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("n")]
        public int Number { get; set; }
        [JsonPropertyName("h")]
        public string Home { get; set; }
        [JsonPropertyName("o")]
        public string HomeOwner { get; set; }
        [JsonPropertyName("a")]
        public string Away { get; set; }
        [JsonPropertyName("u")]
        public string AwayOwner { get; set; }
        [JsonPropertyName("r")]
        public GameResult? Result { get; set; }
    }
}
