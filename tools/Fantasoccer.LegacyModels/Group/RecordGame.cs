using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RecordGame
    {
        [JsonPropertyName("g")]
        public CalendarGame Game { get; set; }
        [JsonPropertyName("y")]
        public int Year { get; set; }
    }
}
