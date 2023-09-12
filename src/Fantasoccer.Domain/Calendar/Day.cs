using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Day
    {
        [JsonPropertyName("a")]
        public int SerieADay { get; set; }
        [JsonPropertyName("n")]
        public int Number { get; set; }
        [JsonPropertyName("g")]
        public List<CalendarGame> Games { get; set; }
    }
}
