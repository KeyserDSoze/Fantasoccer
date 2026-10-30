using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class YearDayLeague
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
        [JsonPropertyName("g")]
        public string Group { get; set; }
        [JsonPropertyName("l")]
        public string League { get; set; }
    }
}
