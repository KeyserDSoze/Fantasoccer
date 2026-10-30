using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class YearDayKey
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
    }
}
