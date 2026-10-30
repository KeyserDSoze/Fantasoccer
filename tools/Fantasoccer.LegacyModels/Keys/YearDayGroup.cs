using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class YearDayGroup
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
        [JsonPropertyName("g")]
        public string Group { get; set; }
    }
}
