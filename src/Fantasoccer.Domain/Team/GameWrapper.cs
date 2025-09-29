using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class GameWrapper
    {
        [JsonPropertyName("t")]
        public List<LiveTeam>? Teams { get; set; }
        [JsonPropertyName("g")]
        public CalendarGame? Game { get; set; }
        [JsonPropertyName("s")]
        public int SerieADay { get; set; }
    }

}
